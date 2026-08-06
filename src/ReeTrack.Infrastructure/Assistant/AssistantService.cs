using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Options;

namespace ReeTrack.Infrastructure.Assistant;

public sealed class AssistantService : IAssistantService
{
    private readonly IChatClient _chatClient;
    private readonly AssistantTools _assistantTools;
    private readonly TimeEntryAssistantTools _timeEntryTools;
    private readonly IOptions<LlmOptions> _llmOptions;
    private readonly ILogger<AssistantService> _logger;

    private const int MaxRetries = 3;
    private static readonly int[] BackoffDelaysMs = [1000, 2000, 4000];

    /// <summary>
    /// How many prior turns to replay. The live draft is re-sent every turn inside
    /// &lt;current_time_entry_draft&gt;/&lt;current_project_draft&gt;, so older turns cost
    /// prompt tokens (and latency) without carrying information the model still needs.
    /// </summary>
    private const int MaxHistoryMessages = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string ProjectSystemPrompt =
        "You are a project creation assistant for ReeTrack, a time tracking application.\n" +
        "The user will describe a project in natural language and you will help create it.\n\n" +
        "Your job is to:\n" +
        "1. Extract or infer project fields from the user's description.\n" +
        "2. Ask clarifying questions when information is missing or ambiguous.\n" +
        "3. Propose a draft when you have enough information.\n\n" +
        "You have access to the following tools:\n" +
        "- SearchClients: Find existing clients by name. Use this to resolve client references.\n" +
        "- SearchProjects: Find existing projects by name or client name.\n" +
        "- SubmitDraft: Submit a complete project draft for user review. Call this when you have all required information.\n" +
        "- ClearDraft: Discard the current draft when the user's message is not about projects.\n\n" +
        "Rules:\n" +
        "- Be conversational, polite, and helpful.\n" +
        "- Currency defaults to \"EUR\" unless the user specifies otherwise.\n" +
        "- Hourly rate, fixed fee, time estimate, and colour are optional — only include when known.\n" +
        "- Always use SearchClients to find the client before submitting a draft. Do not guess client IDs.\n" +
        "- When the user mentions a client name, search for it first to get the correct ID.\n" +
        "- If a client has already been resolved via UI mention (you will see it in the system messages), use it directly without calling SearchClients.\n" +
        "- Propose 3-5 logical tasks based on the project description. Each task needs a name and estimated hours.\n" +
        "- The user may provide their own tasks, hourly rates, fees etc. If so, use those. Otherwise infer from context.\n" +
        "- If the user requests changes to tasks (add, remove, rename, adjust hours), update the draft accordingly and call SubmitDraft with the complete updated draft.\n" +
        "- When the latest user message contains a <current_project_draft> block, that JSON is the source of truth — including any edits the user made in the UI form. Ignore older draft details from chat history. Call SubmitDraft with the COMPLETE updated draft; preserve fields the user did not ask to change.\n" +
        "- While a current draft is present, do NOT call ClearDraft unless the user explicitly asks to discard it.\n" +
        "- If there is no current draft and the user's message is not related to project creation, call ClearDraft and reply conversationally.\n" +
        "- If you are still gathering information, ask questions and do NOT call SubmitDraft yet.\n";

    public AssistantService(
        IChatClient chatClient,
        AssistantTools assistantTools,
        TimeEntryAssistantTools timeEntryTools,
        IOptions<LlmOptions> llmOptions,
        ILogger<AssistantService> logger)
    {
        _chatClient = chatClient;
        _assistantTools = assistantTools;
        _timeEntryTools = timeEntryTools;
        _llmOptions = llmOptions;
        _logger = logger;
    }

    public IAsyncEnumerable<AssistantEvent> StreamChatAsync(
        AssistantChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Mode == AssistantMode.TimeEntry
            ? StreamTimeEntryChatAsync(request, cancellationToken)
            : StreamProjectChatAsync(request, cancellationToken);
    }

    private async IAsyncEnumerable<AssistantEvent> StreamProjectChatAsync(
        AssistantChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");

        _assistantTools.Reset();
        _assistantTools.SeedBaseDraft(request.CurrentDraft);

        var messages = BuildProjectMessages(request);
        var chatOptions = BuildChatOptions(_assistantTools.ToToolList());

        await foreach (var token in StreamTokensAsync(messages, chatOptions, new StringBuilder(), cancellationToken))
            yield return token;

        if (_assistantTools.CapturedDraft is not null)
        {
            yield return new AssistantEvent.DraftEvent(_assistantTools.CapturedDraft);
            yield return new AssistantEvent.DoneEvent(conversationId);
        }
        else if (_assistantTools.DraftCleared)
        {
            yield return new AssistantEvent.DoneEvent(conversationId, DraftCleared: true);
        }
        else
        {
            yield return new AssistantEvent.DoneEvent(conversationId);
        }
    }

    private async IAsyncEnumerable<AssistantEvent> StreamTimeEntryChatAsync(
        AssistantChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");

        _timeEntryTools.Reset();
        _timeEntryTools.SetReferenceDate(AssistantDateContext.ResolveReferenceDate(request.ReferenceDate));
        _timeEntryTools.SeedBaseDraft(request.CurrentTimeEntryDraft);

        foreach (var mention in request.Mentions ?? [])
            _timeEntryTools.RegisterMention(mention.Type, mention.Id, mention.Name, mention.ProjectId, mention.ProjectName);

        var messages = BuildTimeEntryMessages(request);
        var chatOptions = BuildChatOptions(_timeEntryTools.ToToolList());

        var responseText = new StringBuilder();

        await foreach (var token in StreamTokensAsync(messages, chatOptions, responseText, cancellationToken))
            yield return token;

        // Models often claim "created/logged" without calling a submit tool. Only a captured
        // draft populates the UI panel — correct the chat text when that didn't happen.
        // The claim has already been streamed to the user by now, so this appends a
        // retraction rather than replacing the text as it used to.
        if (_timeEntryTools.CapturedDraft is null
            && !_timeEntryTools.DraftCleared
            && LooksLikeFalseDraftClaim(responseText.ToString()))
        {
            yield return new AssistantEvent.TokenEvent(
                "\n\n**Correction:** nothing was drafted or logged just now — ignore the above. " +
                "For a whole week, try \"1 hour every weekday next week from 09:00 to 10:00 on @YourProject\".");
        }

        if (_timeEntryTools.CapturedDraft is not null)
        {
            yield return new AssistantEvent.TimeEntryDraftEvent(_timeEntryTools.CapturedDraft);
            yield return new AssistantEvent.DoneEvent(conversationId);
        }
        else if (_timeEntryTools.DraftCleared)
        {
            yield return new AssistantEvent.DoneEvent(conversationId, DraftCleared: true);
        }
        else
        {
            yield return new AssistantEvent.DoneEvent(conversationId);
        }
    }

    private static bool LooksLikeFalseDraftClaim(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return false;

        // Match common hallucinated success phrasing without punishing clarifying questions.
        return System.Text.RegularExpressions.Regex.IsMatch(
            responseText,
            @"\b(all set|i('ve| have)? (created|logged|submitted|drafted|prepared)|entries? (have been|were) (created|logged|submitted)|draft (is|has been) (ready|submitted|created))\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    // OPENAI001: ReasoningEffortLevel is still marked experimental in OpenAI 2.12.0. It is a
    // stable field of the OpenAI-compatible wire format Groq serves, and an unsupported value
    // would surface immediately as a 400 rather than silently misbehaving.
#pragma warning disable OPENAI001
    private ChatOptions BuildChatOptions(IList<AITool> tools)
    {
        var llm = _llmOptions.Value;

        var options = new ChatOptions
        {
            Temperature = 0.3f,
            Tools = tools,
            MaxOutputTokens = llm.MaxOutputTokens > 0 ? llm.MaxOutputTokens : null,
        };

        if (ResolveReasoningEffort(llm.ReasoningEffort) is { } effort)
        {
            // reasoning_effort is a provider knob with no portable Microsoft.Extensions.AI
            // surface. The OpenAI adapter takes whatever this factory returns as the base
            // request and then layers the options above onto it.
            options.RawRepresentationFactory = _ => new OpenAI.Chat.ChatCompletionOptions
            {
                ReasoningEffortLevel = effort,
            };
        }

        return options;
    }

    private static OpenAI.Chat.ChatReasoningEffortLevel? ResolveReasoningEffort(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "low" => OpenAI.Chat.ChatReasoningEffortLevel.Low,
            "medium" => OpenAI.Chat.ChatReasoningEffortLevel.Medium,
            "high" => OpenAI.Chat.ChatReasoningEffortLevel.High,
            _ => null,
        };
#pragma warning restore OPENAI001

    /// <summary>
    /// Streams the completion as it arrives, one event per delta, appending the full text to
    /// <paramref name="accumulated"/> for callers that need to inspect the finished reply.
    /// </summary>
    /// <remarks>
    /// The producer runs on a channel rather than inline because C# forbids <c>yield return</c>
    /// inside a <c>try</c> that has a <c>catch</c> — which is what previously forced the whole
    /// completion to be buffered and emitted as a single token, so nothing reached the browser
    /// until the entire tool loop had finished.
    /// </remarks>
    private async IAsyncEnumerable<AssistantEvent> StreamTokensAsync(
        IList<ChatMessage> messages,
        ChatOptions options,
        StringBuilder accumulated,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        var producer = PumpCompletionAsync(messages, options, accumulated, channel.Writer, ct);

        await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
            yield return new AssistantEvent.TokenEvent(chunk);

        await producer;
    }

    /// <summary>
    /// Runs the completion with retries and writes every streamed delta to <paramref name="writer"/>.
    /// Only throws on cancellation: a transport failure becomes plain fallback text so the caller
    /// can still close out the SSE stream with a done event.
    /// </summary>
    private async Task PumpCompletionAsync(
        IList<ChatMessage> messages,
        ChatOptions options,
        StringBuilder accumulated,
        ChannelWriter<string> writer,
        CancellationToken ct)
    {
        Exception? failure = null;

        try
        {
            for (var attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, options, ct))
                    {
                        if (string.IsNullOrEmpty(update.Text))
                            continue;

                        accumulated.Append(update.Text);
                        await writer.WriteAsync(update.Text, ct);
                    }

                    return;
                }
                // Retry only while nothing has reached the user yet — replaying the call after
                // partial output would duplicate visible text mid-sentence.
                catch (Exception ex) when (attempt < MaxRetries && accumulated.Length == 0 && IsRetryable(ex))
                {
                    var delay = BackoffDelaysMs[attempt];
                    _logger.LogWarning(ex, "AI service error (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                        attempt + 1, MaxRetries + 1, delay);
                    await Task.Delay(delay, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failure = ex;
                    _logger.LogError(ex, "AI service request failed.");
                    break;
                }
            }

            if (failure is null)
                _logger.LogError("AI service unavailable after {MaxRetries} retries.", MaxRetries);

            var fallback = accumulated.Length > 0
                ? "\n\nSorry — the connection dropped part-way through that reply. Please try again."
                : failure is null
                    ? "I'm sorry, the AI service is temporarily unavailable. Please try again in a moment."
                    : "I'm sorry, something went wrong. Could you try again?";

            accumulated.Append(fallback);
            await writer.WriteAsync(fallback, ct);
        }
        catch (OperationCanceledException)
        {
            // The client disconnected or the request was aborted — nothing left to report.
        }
        finally
        {
            writer.Complete();
        }
    }

    private static bool IsRetryable(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            return httpEx.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                or System.Net.HttpStatusCode.TooManyRequests
                or System.Net.HttpStatusCode.GatewayTimeout
                or System.Net.HttpStatusCode.RequestTimeout;
        }

        return ex is TimeoutException
            or IOException
            or System.Net.Sockets.SocketException;
    }

    private static string BuildTimeEntrySystemPrompt(AssistantChatRequest request)
    {
        var today = AssistantDateContext.ResolveReferenceDate(request.ReferenceDate);
        var calendar = AssistantDateContext.BuildPromptBlock(
            today,
            request.TimeZone,
            request.ReferenceDateTime);

        return
            "You are ReeTrack's time-logging assistant. The user describes work they did; " +
            "you turn it into one or more draft time entries.\n\n" +

            "## Calendar (the user's local timezone — use this, not your own notion of \"today\")\n" +
            calendar + "\n\n" +

            "## Tools\n" +
            "- SearchProjects / SearchTasks / SearchTags — resolve a name to an id.\n" +
            "- SubmitWeekTimeEntryDraft — one row per day across a named week; the server fills in the dates.\n" +
            "- SubmitTimeEntryDraft — the complete list of rows, for explicit dates.\n" +
            "- ClearDraft — discard the current draft.\n\n" +

            "## Which tool to call\n" +
            "| The user's message | Do this |\n" +
            "| --- | --- |\n" +
            "| Spans this/next/last week (\"every day next week\", \"fill in this week\", \"Mon–Wed next week\") | " +
            "SubmitWeekTimeEntryDraft with expandWeek=this\\|next\\|last and expandDays=weekdays (the default when " +
            "weekends aren't mentioned), all, or a weekday list like monday,wednesday,friday. Never invent the dates yourself. |\n" +
            "| Names explicit dates or single days | SubmitTimeEntryDraft with ONE row per calendar date — never one row spanning several days. |\n" +
            "| Changes the current draft (add, remove, retime, re-assign) | SubmitTimeEntryDraft with the COMPLETE list, edits applied, untouched rows preserved. |\n" +
            "| Isn't about logging time | ClearDraft, then reply conversationally. |\n" +
            "| Is missing something you can't infer | Ask one short question and submit nothing. |\n\n" +

            "## Hard rules\n" +
            "1. Every time you emit or mention is the user's LOCAL wall-clock value — entryDate as yyyy-MM-dd, " +
            "startTime/endTime as 24-hour HH:mm. Never convert to UTC, apply an offset, or append \"Z\"; the app " +
            "converts on save. If the user names another timezone, interpret it against theirs and still emit local values.\n" +
            "2. Submitting a draft does NOT save anything. Never say entries were created, logged, or saved — say the " +
            "draft is ready in the panel and the user must click Create.\n" +
            "3. Never invent ids. Resolve each project/task/tag with a Search* tool first — unless a system message " +
            "above already gave you the id from a UI mention, in which case use it directly and skip the search.\n" +
            "4. Omit startTime and endTime when the user only gave an amount of time (\"2 hours\") — that's a " +
            "duration-only entry. Include both only for real clock times or a range.\n" +
            "5. Weeks run Monday–Sunday. \"next week\" is always the next full Monday–Sunday block — never the rest of " +
            "this week, never Sunday-start. A bare weekday name means the Next occurrence listed above, unless the " +
            "user says \"last\" or \"this past\".\n" +
            "6. When the latest user message carries a <current_time_entry_draft> block, that JSON is the source of " +
            "truth — it already includes edits the user made in the form. Ignore older draft details from the chat " +
            "history, and don't ClearDraft while it's present unless the user explicitly asks to discard it.\n\n" +

            "## Style\n" +
            "Brief, friendly, concrete. In a sentence or two, say what you drafted: how many rows, which days, what times.\n";
    }

    private static List<ChatMessage> BuildProjectMessages(AssistantChatRequest request)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, ProjectSystemPrompt) };

        if (request.Mentions is { Count: > 0 })
        {
            var clientMentions = request.Mentions.Where(m =>
                m.Type.Equals("client", StringComparison.OrdinalIgnoreCase)).ToList();

            if (clientMentions.Count > 1)
            {
                var first = clientMentions[0];
                clientMentions = [first];
            }

            foreach (var mention in clientMentions)
            {
                messages.Add(new ChatMessage(ChatRole.System,
                    $"The user explicitly selected this client via the UI — use it directly, " +
                    $"do not call SearchClients for it: clientId={mention.Id}, name=\"{mention.Name}\"."));
            }
        }

        AppendHistory(messages, request);

        AppendUserTurnWithDraft(
            messages,
            request.Message,
            request.CurrentDraft is null ? null : JsonSerializer.Serialize(request.CurrentDraft, JsonOptions),
            "current_project_draft");

        return messages;
    }

    private static List<ChatMessage> BuildTimeEntryMessages(AssistantChatRequest request)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, BuildTimeEntrySystemPrompt(request)) };

        if (request.Mentions is { Count: > 0 })
        {
            // Tags are legitimately plural; unlike project mode's single-client mention,
            // every project/task/tag mention gets its own system line.
            var relevantMentions = request.Mentions.Where(m =>
                m.Type.Equals("project", StringComparison.OrdinalIgnoreCase)
                || m.Type.Equals("task", StringComparison.OrdinalIgnoreCase)
                || m.Type.Equals("tag", StringComparison.OrdinalIgnoreCase));

            foreach (var mention in relevantMentions)
            {
                var toolName = mention.Type.ToLowerInvariant() switch
                {
                    "project" => "SearchProjects",
                    "task" => "SearchTasks",
                    _ => "SearchTags"
                };

                // A task mention names its project too, so the model can fill both ids in one
                // shot rather than following up with SearchProjects.
                var owner = mention.ProjectId is Guid ownerId && ownerId != Guid.Empty
                    ? $" It belongs to projectId={ownerId}, project name=\"{mention.ProjectName}\" — use that as the entry's project."
                    : string.Empty;

                messages.Add(new ChatMessage(ChatRole.System,
                    $"The user explicitly selected this {mention.Type} via the UI — use it directly, " +
                    $"do not call {toolName} for it: id={mention.Id}, name=\"{mention.Name}\".{owner}"));
            }
        }

        AppendHistory(messages, request);

        AppendUserTurnWithDraft(
            messages,
            request.Message,
            request.CurrentTimeEntryDraft is null ? null : JsonSerializer.Serialize(request.CurrentTimeEntryDraft, JsonOptions),
            "current_time_entry_draft");

        return messages;
    }

    private static void AppendHistory(List<ChatMessage> messages, AssistantChatRequest request)
    {
        // Keep only the most recent turns — see MaxHistoryMessages.
        var recent = request.History.Count > MaxHistoryMessages
            ? request.History.Skip(request.History.Count - MaxHistoryMessages)
            : request.History;

        foreach (var msg in recent)
        {
            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? ChatRole.Assistant
                : ChatRole.User;

            messages.Add(new ChatMessage(role, msg.Content));
        }
    }

    // Embed the live UI draft in the latest user turn (not a mid-thread system
    // message) so Groq/OpenAI-compatible models reliably see form edits.
    private static void AppendUserTurnWithDraft(
        List<ChatMessage> messages,
        string? message,
        string? draftJson,
        string draftTag)
    {
        if (string.IsNullOrWhiteSpace(message) && draftJson is null)
            return;

        var userContent = message?.Trim() ?? string.Empty;

        if (draftJson is not null)
        {
            var draftBlock = $"<{draftTag}>\n{draftJson}\n</{draftTag}>";
            userContent = string.IsNullOrEmpty(userContent)
                ? draftBlock
                : $"{userContent}\n\n{draftBlock}";
        }

        if (!string.IsNullOrWhiteSpace(userContent))
            messages.Add(new ChatMessage(ChatRole.User, userContent));
    }
}

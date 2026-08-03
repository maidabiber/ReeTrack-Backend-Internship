using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Assistant;

public sealed class AssistantService : IAssistantService
{
    private readonly IChatClient _chatClient;
    private readonly AssistantTools _assistantTools;
    private readonly ILogger<AssistantService> _logger;

    private const int MaxRetries = 3;
    private static readonly int[] BackoffDelaysMs = [1000, 2000, 4000];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string SystemPrompt =
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
        ILogger<AssistantService> logger)
    {
        _chatClient = chatClient;
        _assistantTools = assistantTools;
        _logger = logger;
    }

    public async IAsyncEnumerable<AssistantEvent> StreamChatAsync(
        AssistantChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");

        _assistantTools.Reset();
        _assistantTools.SeedBaseDraft(request.CurrentDraft);

        var messages = BuildMessages(request);

        var chatOptions = new ChatOptions
        {
            Temperature = 0.3f,
            Tools = _assistantTools.ToToolList(),
        };

        var responseText = await CallStreamingWithRetryAsync(messages, chatOptions, cancellationToken);

        yield return new AssistantEvent.TokenEvent(responseText);

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

    private async Task<string> CallStreamingWithRetryAsync(
        IList<ChatMessage> messages,
        ChatOptions options,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var responseBuilder = new StringBuilder();

                await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, options, ct))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                        responseBuilder.Append(update.Text);
                }

                return responseBuilder.ToString();
            }
            catch (Exception ex) when (attempt < MaxRetries && IsRetryable(ex))
            {
                var delay = BackoffDelaysMs[attempt];
                _logger.LogWarning(ex, "AI service error (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                    attempt + 1, MaxRetries + 1, delay);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "AI service request failed.");
                return "I'm sorry, something went wrong. Could you try again?";
            }
        }

        _logger.LogError("AI service unavailable after {MaxRetries} retries.", MaxRetries);
        return "I'm sorry, the AI service is temporarily unavailable. Please try again in a moment.";
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

    private static List<ChatMessage> BuildMessages(AssistantChatRequest request)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, SystemPrompt) };

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

        foreach (var msg in request.History)
        {
            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? ChatRole.Assistant
                : ChatRole.User;

            messages.Add(new ChatMessage(role, msg.Content));
        }

        // Embed the live UI draft in the latest user turn (not a mid-thread system
        // message) so Groq/OpenAI-compatible models reliably see form edits.
        if (!string.IsNullOrWhiteSpace(request.Message) || request.CurrentDraft is not null)
        {
            var userContent = request.Message?.Trim() ?? string.Empty;

            if (request.CurrentDraft is not null)
            {
                var draftJson = JsonSerializer.Serialize(request.CurrentDraft, JsonOptions);
                var draftBlock =
                    "<current_project_draft>\n" +
                    draftJson + "\n" +
                    "</current_project_draft>";

                userContent = string.IsNullOrEmpty(userContent)
                    ? draftBlock
                    : $"{userContent}\n\n{draftBlock}";
            }

            if (!string.IsNullOrWhiteSpace(userContent))
                messages.Add(new ChatMessage(ChatRole.User, userContent));
        }

        return messages;
    }
}

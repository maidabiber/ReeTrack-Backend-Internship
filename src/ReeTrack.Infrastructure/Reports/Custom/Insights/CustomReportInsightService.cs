using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Application.Common.Options;

namespace ReeTrack.Infrastructure.Reports.Custom.Insights;

/// <summary>
/// Writes commentary on an evaluated custom report.
/// </summary>
/// <remarks>
/// The model never emits a figure. It returns qualitative headlines plus a reference to the
/// block, row, and column it is talking about, and this service reads the actual number out of
/// the report IR. A reference that does not resolve is dropped, so an invented client or metric
/// cannot reach the page — which matters because these reports are exported to clients.
/// </remarks>
public sealed class CustomReportInsightService : ICustomReportInsightService
{
    private const string SchemaName = "report_insights";
    private const int MaxFindings = 5;
    private static readonly TimeSpan ModelTimeout = TimeSpan.FromSeconds(30);

    // Groq's constrained decoding is unreliable with union types; every field is a string and
    // the nullable ones use the empty string, matching SmartTimeParseService.
    private static readonly BinaryData ResponseSchema = BinaryData.FromBytes("""
        {
          "type": "object",
          "properties": {
            "findings": {
              "type": "array",
              "description": "Between one and five observations, most important first.",
              "items": {
                "type": "object",
                "properties": {
                  "headline": {
                    "type": "string",
                    "description": "One sentence, qualitative only. Never write a number, percentage, or currency amount - the figure is attached automatically from the report."
                  },
                  "block_id": {
                    "type": "string",
                    "description": "Exact id of the BLOCK this observation is about, copied from the data."
                  },
                  "row_key": {
                    "type": "string",
                    "description": "Exact row= key from that block when the observation is about one row. Empty string for a KPI block or a whole-block statement."
                  },
                  "column_key": {
                    "type": "string",
                    "description": "Exact column key from that block that carries the figure. Empty string if the observation cites no single figure."
                  }
                },
                "required": ["headline", "block_id", "row_key", "column_key"],
                "additionalProperties": false
              }
            }
          },
          "required": ["findings"],
          "additionalProperties": false
        }
        """u8.ToArray());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ICustomReportService _reports;
    private readonly LlmOptions _options;
    private readonly ILogger<CustomReportInsightService> _logger;

    public CustomReportInsightService(
        ICustomReportService reports,
        IOptions<LlmOptions> options,
        ILogger<CustomReportInsightService> logger)
    {
        _reports = reports;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CustomReportInsightsDto> GenerateAsync(
        CustomReportSpec spec,
        string blockId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var block = spec.Blocks.OfType<NarrativeBlockSpec>()
            .FirstOrDefault(candidate => string.Equals(candidate.Id, blockId, StringComparison.Ordinal))
            ?? throw AppErrors.Validation($"No narrative block '{blockId}' on this report.");

        EnsureConfigured();

        var report = await _reports.GetOrRunAsync(spec, cancellationToken);
        var facts = InsightFacts.From(report);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ModelTimeout);

        var findings = await RequestFindingsAsync(facts, block.Focus, timeout.Token, cancellationToken);
        var paragraphs = Render(findings, facts);

        if (paragraphs.Count == 0)
        {
            throw new AppException(
                "The model did not return any usable observations about this report.",
                502,
                ErrorCode.ServiceUnavailable);
        }

        return new CustomReportInsightsDto
        {
            BlockId = block.Id,
            Paragraphs = paragraphs,
            GeneratedAtUtc = DateTime.UtcNow,
            Fingerprint = CustomReportFingerprint.Compute(spec)
        };
    }

    /// <summary>Attaches each finding's figure from the IR, dropping references that do not resolve.</summary>
    private List<string> Render(IReadOnlyList<LlmFinding> findings, InsightFacts facts)
    {
        var paragraphs = new List<string>();

        foreach (var finding in findings.Take(MaxFindings))
        {
            var headline = finding.Headline?.Trim();
            if (string.IsNullOrWhiteSpace(headline))
                continue;

            var figure = facts.ResolveReference(
                finding.BlockId,
                NullIfBlank(finding.RowKey),
                NullIfBlank(finding.ColumnKey));

            if (figure is null && !string.IsNullOrWhiteSpace(finding.ColumnKey))
            {
                // The model pointed at something that is not in the report. Keeping the
                // sentence without its figure would imply evidence that does not exist.
                _logger.LogInformation(
                    "Dropped a report insight referencing block {BlockId} column {ColumnKey}, which the report does not contain.",
                    finding.BlockId,
                    finding.ColumnKey);
                continue;
            }

            paragraphs.Add(figure is null ? headline : $"{headline} ({figure})");
        }

        return paragraphs;
    }

    private async Task<IReadOnlyList<LlmFinding>> RequestFindingsAsync(
        InsightFacts facts,
        string? focus,
        CancellationToken modelToken,
        CancellationToken callerToken)
    {
        var client = CreateChatClient();
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: SchemaName,
                jsonSchema: ResponseSchema,
                jsonSchemaIsStrict: true)
        };

        ChatCompletion completion;
        try
        {
            completion = await client.CompleteChatAsync(BuildMessages(facts, focus), options, modelToken);
        }
        catch (ClientResultException ex) when (ex.Status is 401 or 403)
        {
            _logger.LogError(ex, "LLM authentication failed while generating report insights.");
            throw new AppException("Report insights are misconfigured.", 503, ErrorCode.ServiceUnavailable);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogWarning(ex, "LLM rate limit hit while generating report insights.");
            throw new AppException("Report insights are temporarily unavailable. Please try again.", 503, ErrorCode.ServiceUnavailable);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach LLM endpoint {BaseUrl}.", _options.BaseUrl);
            throw new AppException("Could not reach the insights provider.", 503, ErrorCode.ServiceUnavailable);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Report insight generation exceeded {Timeout}.", ModelTimeout);
            throw new AppException("Generating insights took too long. Please try again.", 504, ErrorCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling the LLM for report insights.");
            throw new AppException("Could not generate insights. Please try again.", 502, ErrorCode.ServiceUnavailable);
        }

        if (completion.Content.Count == 0 || string.IsNullOrWhiteSpace(completion.Content[0].Text))
        {
            _logger.LogWarning("LLM returned an empty insights response.");
            throw new AppException("Report insights came back empty.", 502, ErrorCode.ServiceUnavailable);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<LlmInsights>(completion.Content[0].Text, JsonOptions);
            return payload?.Findings ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize LLM insights payload: {Payload}", completion.Content[0].Text);
            throw new AppException("Report insights came back in an unexpected shape.", 502, ErrorCode.ServiceUnavailable);
        }
    }

    private static List<ChatMessage> BuildMessages(InsightFacts facts, string? focus)
    {
        var system =
            "You comment on an already-computed time-tracking report for an agency's admins. " +
            $"Return at most {MaxFindings} findings, most decision-relevant first. " +
            "Each headline is ONE qualitative sentence and must contain no numbers, percentages, " +
            "or currency amounts — the figure is attached automatically from the report data. " +
            "Point each finding at a real block_id, row_key, and column_key copied exactly from " +
            "the data below. Never invent a client, project, member, or metric that is not listed. " +
            (facts.HasComparison
                ? "Previous-period values are given; prefer findings about what changed."
                : "There is no comparison period, so describe composition and concentration rather than trends.");

        var user = string.IsNullOrWhiteSpace(focus)
            ? $"Report data:\n{facts.Digest}"
            : $"The reader cares most about: {focus.Trim()}\n\nReport data:\n{facts.Digest}";

        return [new SystemChatMessage(system), new UserChatMessage(user)];
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(ResolveApiKey()))
            throw new AppException("Report insights are not configured (missing Llm:ApiKey / GROQ_API_KEY).", 503, ErrorCode.ServiceUnavailable);

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new AppException("Report insights are not configured (missing Llm:BaseUrl).", 503, ErrorCode.ServiceUnavailable);
    }

    private string ResolveApiKey() =>
        string.IsNullOrWhiteSpace(_options.ApiKey)
            ? Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? string.Empty
            : _options.ApiKey;

    private ChatClient CreateChatClient() =>
        new(
            _options.Model,
            new ApiKeyCredential(ResolveApiKey()),
            new OpenAIClientOptions { Endpoint = new Uri(_options.BaseUrl) });

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class LlmInsights
    {
        [JsonPropertyName("findings")]
        public List<LlmFinding> Findings { get; set; } = [];
    }

    private sealed class LlmFinding
    {
        [JsonPropertyName("headline")]
        public string? Headline { get; set; }

        [JsonPropertyName("block_id")]
        public string? BlockId { get; set; }

        [JsonPropertyName("row_key")]
        public string? RowKey { get; set; }

        [JsonPropertyName("column_key")]
        public string? ColumnKey { get; set; }
    }
}

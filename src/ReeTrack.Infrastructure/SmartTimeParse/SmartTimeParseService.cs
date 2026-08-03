using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.SmartTimeParse;

public sealed class SmartTimeParseService : ISmartTimeParseService
{
    private const string SchemaName = "parsed_time_entry";
    private static readonly Regex TimeOfDayPattern = new(
        @"^(?:[01]\d|2[0-3]):[0-5]\d$",
        RegexOptions.Compiled);
    private static readonly Regex IsoDatePattern = new(
        @"^\d{4}-\d{2}-\d{2}$",
        RegexOptions.Compiled);

    private static readonly JsonElement ResponseSchema = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            description = new
            {
                type = "string",
                description = "Cleaned activity description. Remove duration, clock times, dates, project/task/tag names, and billable terms."
            },
            duration_minutes = new
            {
                type = "string",
                description = "Total duration in minutes as a numeric string, e.g. \"120\". Use \"0\" if unknown."
            },
            matched_project_id = new
            {
                type = new[] { "string", "null" },
                description = "Exact id of the best-matching project from the provided list, or null."
            },
            matched_project_task_id = new
            {
                type = new[] { "string", "null" },
                description = "Exact id of the best-matching open task from the provided list (must belong to matched_project_id when both set), or null."
            },
            matched_tag_ids = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Exact ids of matching tags from the provided list. Empty array if none."
            },
            is_billable = new
            {
                type = "string",
                description = "\"true\" or \"false\". Use \"false\" for non-billable/internal/unpaid work; otherwise \"true\"."
            },
            start_time = new
            {
                type = new[] { "string", "null" },
                description = "Local start time as HH:mm (24h) when a clock time or range is present; otherwise null."
            },
            end_time = new
            {
                type = new[] { "string", "null" },
                description = "Local end time as HH:mm (24h) when a range/end time is present; otherwise null."
            },
            entry_date = new
            {
                type = new[] { "string", "null" },
                description = "Calendar date as YYYY-MM-DD when a date or relative day is present; otherwise null."
            },
            confidence_score = new
            {
                type = "string",
                description = "Overall confidence from 0.0 to 1.0 as a numeric string, e.g. \"0.85\"."
            }
        },
        required = new[]
        {
            "description", "duration_minutes", "matched_project_id", "matched_project_task_id",
            "matched_tag_ids", "is_billable", "start_time", "end_time", "entry_date", "confidence_score"
        },
        additionalProperties = false,
    });

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IChatClient _chatClient;
    private readonly ILogger<SmartTimeParseService> _logger;

    public SmartTimeParseService(
        IChatClient chatClient,
        ILogger<SmartTimeParseService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<ParsedTimeEntryDto> ParseAsync(
        string userInput,
        SmartTimeParseCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var trimmed = userInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw AppErrors.Validation("Time entry text is required.");

        var messages = BuildMessages(trimmed, catalog);
        var chatOptions = new ChatOptions
        {
            ResponseFormat = new ChatResponseFormatJson(
                schema: ResponseSchema,
                schemaName: SchemaName,
                schemaDescription: "Structured time entry parsed from user input"),
        };

        ChatResponse completion;
        try
        {
            completion = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach the AI service while parsing time entry.");
            throw new AppException(
                "Could not reach the AI service. Check network connectivity and Llm:BaseUrl.",
                503,
                ErrorCode.ServiceUnavailable);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling LLM for time entry parse.");
            throw new AppException(
                "Smart time parsing failed. Please try again.",
                502,
                ErrorCode.ServiceUnavailable);
        }

        if (completion.FinishReason == ChatFinishReason.ContentFilter)
            throw AppErrors.Validation("The time entry text could not be processed.");

        var responseText = completion.Text;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            _logger.LogWarning("LLM returned an empty structured response.");
            throw new AppException(
                "Smart time parsing returned an empty result.",
                502,
                ErrorCode.ServiceUnavailable);
        }

        LlmParsedTimeEntry raw;
        try
        {
            raw = JsonSerializer.Deserialize<LlmParsedTimeEntry>(responseText, JsonOptions)
                ?? throw new JsonException("Deserialized payload was null.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize LLM structured output: {Payload}", responseText);
            throw new AppException(
                "Smart time parsing returned an invalid result.",
                502,
                ErrorCode.ServiceUnavailable);
        }

        return Normalize(raw, catalog);
    }

    private static List<ChatMessage> BuildMessages(string userInput, SmartTimeParseCatalog catalog)
    {
        var projectLines = FormatLines(
            catalog.Projects.Select(p => $"- id: {p.Id}, name: {p.Name}"));
        var taskLines = FormatLines(
            catalog.Tasks.Select(t => $"- id: {t.Id}, project_id: {t.ProjectId}, name: {t.Name}"));
        var tagLines = FormatLines(
            catalog.Tags.Select(t => $"- id: {t.Id}, name: {t.Name}"));

        var system = """
            You extract structured time-entry data from a single free-form line of text.

            Rules:
            - description: keep the activity wording; remove duration, clock times, dates, project/task/tag names, and billable/non-billable wording.
            - duration_minutes: convert any duration or time range into total minutes as an integer.
              Examples: "2h" -> 120, "1.5 hours" -> 90, "from 10 to 12" -> 120, "9-10:30" -> 90, "45m" -> 45.
              If only start/end times are present, derive duration from them. If no duration is present, use 0.
            - matched_project_id: must be exactly one of the provided project ids, or null. Never invent an id.
            - matched_project_task_id: must be exactly one of the provided task ids, or null. If both project and task are set, the task must belong to that project.
            - matched_tag_ids: zero or more exact tag ids from the provided list (e.g. hashtags or named tags). Never invent ids.
            - is_billable: default true. Set false for phrases like non-billable, unpaid, internal, admin, training (when clearly non-client work).
            - start_time / end_time: HH:mm 24-hour local times when present (e.g. "10am-12pm" -> "10:00"/"12:00"). Otherwise null.
            - entry_date: YYYY-MM-DD when a date or relative day is mentioned (yesterday, today, Monday, last Friday). Use the reference date provided. Otherwise null.
            - confidence_score: numeric string from "0.0" to "1.0" (e.g. "0.85"). Lower when guessing.
            - duration_minutes: numeric string of total minutes (e.g. "120"). Use "0" if unknown.
            - is_billable: string "true" or "false" only.
            """;

        var user = new StringBuilder();
        user.AppendLine($"Reference date (today): {catalog.ReferenceDate:yyyy-MM-dd}");
        user.AppendLine();
        user.AppendLine("Active projects:");
        user.AppendLine(projectLines);
        user.AppendLine();
        user.AppendLine("Open tasks:");
        user.AppendLine(taskLines);
        user.AppendLine();
        user.AppendLine("Tags:");
        user.AppendLine(tagLines);
        user.AppendLine();
        user.AppendLine("User input:");
        user.Append(userInput);

        return
        [
            new ChatMessage(ChatRole.System, system),
            new ChatMessage(ChatRole.User, user.ToString())
        ];
    }

    private static string FormatLines(IEnumerable<string> lines)
    {
        var list = lines.ToList();
        return list.Count == 0 ? "(none)" : string.Join("\n", list);
    }

    private ParsedTimeEntryDto Normalize(LlmParsedTimeEntry raw, SmartTimeParseCatalog catalog)
    {
        var description = (raw.Description ?? string.Empty).Trim();
        var duration = Math.Max(0, ParseInt(raw.DurationMinutes));
        var confidence = Math.Clamp(ParseDouble(raw.ConfidenceScore), 0.0, 1.0);
        var isBillable = ParseBool(raw.IsBillable, defaultValue: true);

        Guid? matchedProjectId = null;
        if (!string.IsNullOrWhiteSpace(raw.MatchedProjectId)
            && Guid.TryParse(raw.MatchedProjectId, out var projectId)
            && catalog.Projects.Any(p => p.Id == projectId))
        {
            matchedProjectId = projectId;
        }
        else if (!string.IsNullOrWhiteSpace(raw.MatchedProjectId))
        {
            _logger.LogWarning("Discarding unmatched project id from LLM: {ProjectId}", raw.MatchedProjectId);
            confidence = Math.Min(confidence, 0.4);
        }

        Guid? matchedTaskId = null;
        if (!string.IsNullOrWhiteSpace(raw.MatchedProjectTaskId)
            && Guid.TryParse(raw.MatchedProjectTaskId, out var taskId))
        {
            var task = catalog.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task is not null
                && (matchedProjectId is null || task.ProjectId == matchedProjectId))
            {
                matchedTaskId = task.Id;
                matchedProjectId ??= task.ProjectId;
            }
            else
            {
                _logger.LogWarning("Discarding unmatched task id from LLM: {TaskId}", raw.MatchedProjectTaskId);
                confidence = Math.Min(confidence, 0.4);
            }
        }

        var allowedTagIds = catalog.Tags.Select(t => t.Id).ToHashSet();
        var matchedTagIds = new List<Guid>();
        foreach (var rawTagId in raw.MatchedTagIds ?? [])
        {
            if (Guid.TryParse(rawTagId, out var tagId) && allowedTagIds.Contains(tagId))
                matchedTagIds.Add(tagId);
            else if (!string.IsNullOrWhiteSpace(rawTagId))
            {
                _logger.LogWarning("Discarding unmatched tag id from LLM: {TagId}", rawTagId);
                confidence = Math.Min(confidence, 0.5);
            }
        }

        var startTime = NormalizeTime(raw.StartTime);
        var endTime = NormalizeTime(raw.EndTime);
        var entryDate = NormalizeDate(raw.EntryDate);

        if (duration == 0 && startTime is not null && endTime is not null
            && TimeOnly.TryParseExact(startTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            && TimeOnly.TryParseExact(endTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
            && end > start)
        {
            duration = (int)(end - start).TotalMinutes;
        }

        if (matchedProjectId is null && matchedTaskId is null && matchedTagIds.Count == 0)
            confidence = Math.Min(confidence, 0.5);

        return new ParsedTimeEntryDto
        {
            Description = description,
            DurationMinutes = duration,
            MatchedProjectId = matchedProjectId,
            MatchedProjectTaskId = matchedTaskId,
            MatchedTagIds = matchedTagIds,
            IsBillable = isBillable,
            StartTime = startTime,
            EndTime = endTime,
            EntryDate = entryDate,
            ConfidenceScore = confidence
        };
    }

    private static int ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i;

        if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Round(d);

        return 0;
    }

    private static double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : 0;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var trimmed = value.Trim();
        if (bool.TryParse(trimmed, out var b))
            return b;

        return trimmed.ToLowerInvariant() switch
        {
            "1" or "yes" or "y" => true,
            "0" or "no" or "n" => false,
            _ => defaultValue
        };
    }

    private static string? NormalizeTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return TimeOfDayPattern.IsMatch(trimmed) ? trimmed : null;
    }

    private static string? NormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (!IsoDatePattern.IsMatch(trimmed))
            return null;

        return DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? trimmed
            : null;
    }

    private sealed class LlmParsedTimeEntry
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("duration_minutes")]
        public string? DurationMinutes { get; set; }

        [JsonPropertyName("matched_project_id")]
        public string? MatchedProjectId { get; set; }

        [JsonPropertyName("matched_project_task_id")]
        public string? MatchedProjectTaskId { get; set; }

        [JsonPropertyName("matched_tag_ids")]
        public List<string>? MatchedTagIds { get; set; }

        [JsonPropertyName("is_billable")]
        public string? IsBillable { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("entry_date")]
        public string? EntryDate { get; set; }

        [JsonPropertyName("confidence_score")]
        public string? ConfidenceScore { get; set; }
    }
}

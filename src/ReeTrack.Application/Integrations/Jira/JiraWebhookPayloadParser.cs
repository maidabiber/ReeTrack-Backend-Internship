using System.Text.Json;

namespace ReeTrack.Application.Integrations.Jira;

public sealed record JiraWebhookIssueEvent(
    string WebhookEvent,
    string JiraProjectId,
    string JiraProjectKey,
    JiraApiIssue Issue,
    bool IsSubtask);

public static class JiraWebhookPayloadParser
{
    private static readonly HashSet<string> SupportedEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "jira:issue_created",
        "jira:issue_updated"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParse(ReadOnlySpan<byte> payload, out JiraWebhookIssueEvent? parsed)
    {
        parsed = null;

        WebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<WebhookEnvelope>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (envelope is null
            || string.IsNullOrWhiteSpace(envelope.WebhookEvent)
            || !SupportedEvents.Contains(envelope.WebhookEvent))
        {
            return false;
        }

        var issue = envelope.Issue;
        if (issue is null
            || string.IsNullOrWhiteSpace(issue.Id)
            || string.IsNullOrWhiteSpace(issue.Key))
        {
            return false;
        }

        var projectId = issue.Fields?.Project?.Id?.Trim();
        var projectKey = issue.Fields?.Project?.Key?.Trim();
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(projectKey))
            return false;

        var isSubtask = issue.Fields?.Issuetype?.Subtask == true;
        var summary = issue.Fields?.Summary?.Trim();
        if (string.IsNullOrWhiteSpace(summary))
            summary = issue.Key;

        var category = issue.Fields?.Status?.StatusCategory?.Key;
        var isDone = string.Equals(category, "done", StringComparison.OrdinalIgnoreCase);

        decimal? estimateHours = null;
        var originalSeconds = issue.Fields?.Timetracking?.OriginalEstimateSeconds;
        if (originalSeconds is > 0)
            estimateHours = Math.Round(originalSeconds.Value / 3600m, 2);

        parsed = new JiraWebhookIssueEvent(
            envelope.WebhookEvent,
            projectId,
            projectKey,
            new JiraApiIssue(
                issue.Id,
                issue.Key,
                summary,
                isDone,
                issue.Fields?.Assignee?.EmailAddress,
                estimateHours),
            isSubtask);

        return true;
    }

    private sealed class WebhookEnvelope
    {
        public string? WebhookEvent { get; set; }
        public IssueDto? Issue { get; set; }
    }

    private sealed class IssueDto
    {
        public string? Id { get; set; }
        public string? Key { get; set; }
        public IssueFieldsDto? Fields { get; set; }
    }

    private sealed class IssueFieldsDto
    {
        public string? Summary { get; set; }
        public ProjectDto? Project { get; set; }
        public StatusDto? Status { get; set; }
        public AssigneeDto? Assignee { get; set; }
        public TimetrackingDto? Timetracking { get; set; }
        public IssueTypeDto? Issuetype { get; set; }
    }

    private sealed class ProjectDto
    {
        public string? Id { get; set; }
        public string? Key { get; set; }
    }

    private sealed class IssueTypeDto
    {
        public bool Subtask { get; set; }
    }

    private sealed class StatusDto
    {
        public StatusCategoryDto? StatusCategory { get; set; }
    }

    private sealed class StatusCategoryDto
    {
        public string? Key { get; set; }
    }

    private sealed class AssigneeDto
    {
        public string? EmailAddress { get; set; }
    }

    private sealed class TimetrackingDto
    {
        public decimal? OriginalEstimateSeconds { get; set; }
    }
}

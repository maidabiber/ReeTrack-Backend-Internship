namespace ReeTrack.Application.Integrations.Jira;

public interface IJiraApiClient
{
    Task<IReadOnlyList<JiraApiProject>> ListProjectsAsync(
        string siteUrl,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JiraApiIssue>> ListIssuesAsync(
        string siteUrl,
        string email,
        string apiToken,
        string projectKey,
        CancellationToken cancellationToken = default);
}

public sealed record JiraApiProject(string Id, string Key, string Name);

public sealed record JiraApiIssue(
    string Id,
    string Key,
    string Summary,
    bool IsDone,
    string? AssigneeEmail,
    decimal? OriginalEstimateHours);

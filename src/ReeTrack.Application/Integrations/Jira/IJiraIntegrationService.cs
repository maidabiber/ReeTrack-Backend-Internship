using ReeTrack.Application.Integrations.Jira.Models;

namespace ReeTrack.Application.Integrations.Jira;

public interface IJiraIntegrationService
{
    Task<JiraConnectionDto> GetConnectionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JiraRemoteProjectDto>> ListRemoteProjectsAsync(CancellationToken cancellationToken = default);

    Task<IntegrateJiraProjectResult> IntegrateProjectAsync(
        IntegrateJiraProjectInput input,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IntegrateJiraProjectResult> SyncProjectAsync(
        Guid reeTrackProjectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a single Jira issue into an already-integrated project.
    /// Returns false when no matching local project exists.
    /// </summary>
    Task<bool> ApplyRemoteIssueAsync(
        string jiraProjectId,
        string jiraProjectKey,
        JiraApiIssue issue,
        CancellationToken cancellationToken = default);
}

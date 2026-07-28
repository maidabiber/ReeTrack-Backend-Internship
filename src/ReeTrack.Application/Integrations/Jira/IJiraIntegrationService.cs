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
}

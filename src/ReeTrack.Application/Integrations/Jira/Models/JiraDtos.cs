namespace ReeTrack.Application.Integrations.Jira.Models;

/// <summary>Status of env-configured Jira credentials (no secrets returned).</summary>
public sealed record JiraConnectionDto(bool IsConfigured, string? SiteUrl, string? Email);

public sealed record JiraRemoteProjectDto(
    string Id,
    string Key,
    string Name,
    bool IsIntegrated,
    Guid? ReeTrackProjectId,
    Guid? ClientId,
    string? ClientName);

public sealed record IntegrateJiraProjectInput(string JiraProjectId, Guid ClientId);

public sealed record IntegrateJiraProjectResult(
    Guid ProjectId,
    string ProjectName,
    int TasksImported,
    string Message);

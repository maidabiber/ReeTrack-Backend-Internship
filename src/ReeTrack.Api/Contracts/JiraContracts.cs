namespace ReeTrack.Api.Contracts;

public sealed class IntegrateJiraProjectRequest
{
    public string JiraProjectId { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
}

public sealed class JiraConnectionResponse
{
    public required bool IsConfigured { get; init; }
    public required string? SiteUrl { get; init; }
    public required string? Email { get; init; }
}

public sealed class JiraRemoteProjectResponse
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required bool IsIntegrated { get; init; }
    public required Guid? ReeTrackProjectId { get; init; }
    public required Guid? ClientId { get; init; }
    public required string? ClientName { get; init; }
}

public sealed class IntegrateJiraProjectResponse
{
    public required Guid ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int TasksImported { get; init; }
    public required string Message { get; init; }
}

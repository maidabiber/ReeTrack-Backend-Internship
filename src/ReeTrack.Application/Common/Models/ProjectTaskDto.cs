namespace ReeTrack.Application.Common.Models;

public sealed class ProjectTaskDto
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid ClientId { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public string? ProjectName { get; init; }
    public string? ProjectColor { get; init; }
    public string? ClientName { get; init; }
    public required Guid? AssignedToUserId { get; init; }
    public required string? AssignedToName { get; init; }
    public required decimal? TimeEstimateHours { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

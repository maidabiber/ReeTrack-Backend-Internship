namespace ReeTrack.Application.Common.Models;

public sealed class ProjectTaskDto
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public required Guid? AssignedToUserId { get; init; }
    public required string? AssignedToName { get; init; }
    public required decimal? TimeEstimateHours { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

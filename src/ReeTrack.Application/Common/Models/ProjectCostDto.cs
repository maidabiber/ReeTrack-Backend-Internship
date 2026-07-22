namespace ReeTrack.Application.Common.Models;

public sealed class ProjectCostDto
{
    public required Guid ProjectId { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required DateTime CalculatedAtUtc { get; init; }
}

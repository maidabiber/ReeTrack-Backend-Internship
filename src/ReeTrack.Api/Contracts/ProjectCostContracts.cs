namespace ReeTrack.Api.Contracts;

public sealed class ProjectCostResponse
{
    public required Guid ProjectId { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required DateTime CalculatedAtUtc { get; init; }
}

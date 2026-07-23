namespace ReeTrack.Application.Common.Models;

public sealed class ProjectDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid ClientId { get; init; }
    public required string ClientName { get; init; }
    public required string Status { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal? HourlyRate { get; init; }
    public required decimal? FixedFeeAmount { get; init; }
    public required decimal? TimeEstimateHours { get; init; }
    /// <summary>Confirmed tracked hours on this project (and its tasks).</summary>
    public required decimal ActualHours { get; init; }
    public required string? Color { get; init; }
    public required int TaskCount { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

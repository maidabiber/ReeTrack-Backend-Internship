namespace ReeTrack.Application.Common.Models;

public sealed class ReportFilterSetDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ReportQuery Query { get; init; }
    public required int SchemaVersion { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}

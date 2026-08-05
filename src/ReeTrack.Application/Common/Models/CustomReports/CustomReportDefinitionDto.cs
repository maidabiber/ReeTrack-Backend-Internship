using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Models.CustomReports;

public sealed class CustomReportDefinitionDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required CustomReportSpec Spec { get; init; }
    public required int SchemaVersion { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public required CustomReportVisibility Visibility { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public bool CanEdit { get; init; }
}

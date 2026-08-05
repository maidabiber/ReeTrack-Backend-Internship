using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Contracts;

/// <summary>
/// Request body for <c>POST /api/reports/custom/run</c>.
/// The spec model is shared with the application layer so polymorphic block
/// deserialization stays in one place.
/// </summary>
public sealed class CustomReportRunRequest
{
    public required CustomReportSpec Spec { get; init; }
}

public sealed class CustomReportRunResponse
{
    public required ReportKpisResponse Kpis { get; init; }
    public required ReportBasisResponse Basis { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public string? GeneratedByName { get; init; }
    public DateOnly? FirstEntryDate { get; init; }
    public DateOnly? FilterFromDate { get; init; }
    public DateOnly? FilterToDate { get; init; }
    public required IReadOnlyList<ReportBlockResult> Blocks { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public ComparisonPeriodDto? Comparison { get; init; }
}

public sealed class CustomReportCatalogueResponse
{
    public required IReadOnlyList<DimensionCatalogueItemDto> Dimensions { get; init; }
    public required IReadOnlyList<MetricCatalogueItemDto> Metrics { get; init; }
    public required IReadOnlyList<BlockTypeCatalogueItemDto> BlockTypes { get; init; }
    public required IReadOnlyList<EntryColumnCatalogueItemDto> EntryColumns { get; init; }
    public required IReadOnlyList<string> Operators { get; init; }
}

public sealed class SaveCustomReportDefinitionRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public required CustomReportSpec Spec { get; init; }
    public CustomReportVisibility Visibility { get; init; } = CustomReportVisibility.Shared;
}

public sealed class CustomReportDefinitionResponse
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

public sealed class CustomReportInsightsRequest
{
    public required CustomReportSpec Spec { get; init; }

    /// <summary>Id of the narrative block to write commentary for.</summary>
    public required string BlockId { get; init; }
}

public sealed class CustomReportInsightsResponse
{
    public required string BlockId { get; init; }
    public required IReadOnlyList<string> Paragraphs { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public required string Fingerprint { get; init; }
}

using ReeTrack.Application.Common.Models.CustomReports;

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
}

public sealed class CustomReportCatalogueResponse
{
    public required IReadOnlyList<DimensionCatalogueItemDto> Dimensions { get; init; }
    public required IReadOnlyList<MetricCatalogueItemDto> Metrics { get; init; }
    public required IReadOnlyList<BlockTypeCatalogueItemDto> BlockTypes { get; init; }
    public required IReadOnlyList<EntryColumnCatalogueItemDto> EntryColumns { get; init; }
    public required IReadOnlyList<string> Operators { get; init; }
}

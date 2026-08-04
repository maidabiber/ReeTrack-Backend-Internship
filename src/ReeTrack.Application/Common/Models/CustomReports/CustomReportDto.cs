namespace ReeTrack.Application.Common.Models.CustomReports;

public sealed class CustomReportDto
{
    public required ReportKpisDto Kpis { get; init; }
    public required ReportBasisDto Basis { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public string? GeneratedByName { get; init; }
    public DateOnly? FirstEntryDate { get; init; }
    public DateOnly? FilterFromDate { get; init; }
    public DateOnly? FilterToDate { get; init; }
    public required IReadOnlyList<ReportBlockResult> Blocks { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class CustomReportCatalogueDto
{
    public required IReadOnlyList<DimensionCatalogueItemDto> Dimensions { get; init; }
    public required IReadOnlyList<MetricCatalogueItemDto> Metrics { get; init; }
    public required IReadOnlyList<BlockTypeCatalogueItemDto> BlockTypes { get; init; }
    public required IReadOnlyList<EntryColumnCatalogueItemDto> EntryColumns { get; init; }
    public required IReadOnlyList<string> Operators { get; init; }
}

public sealed class DimensionCatalogueItemDto
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required bool FansOut { get; init; }
}

public sealed class MetricCatalogueItemDto
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required MetricUnit Unit { get; init; }
    public required MetricScope Scope { get; init; }
    public required IReadOnlyList<string> CompatibleDimensions { get; init; }
}

public sealed class BlockTypeCatalogueItemDto
{
    public required string Type { get; init; }
    public required string Label { get; init; }
}

public sealed class EntryColumnCatalogueItemDto
{
    public required string Id { get; init; }
    public required string Label { get; init; }
}

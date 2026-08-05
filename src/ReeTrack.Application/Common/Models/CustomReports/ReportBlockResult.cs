using System.Text.Json.Serialization;

namespace ReeTrack.Application.Common.Models.CustomReports;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KpiGroupResult), "kpi")]
[JsonDerivedType(typeof(TableResult), "table")]
[JsonDerivedType(typeof(SeriesResult), "series")]
[JsonDerivedType(typeof(ProseResult), "prose")]
public abstract class ReportBlockResult
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public string? Footnote { get; init; }
}

public sealed class KpiGroupResult : ReportBlockResult
{
    public required IReadOnlyList<KpiCell> Cells { get; init; }
}

public sealed class KpiCell
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public decimal? Value { get; init; }
    public required MetricUnit Unit { get; init; }
    public string? CurrencyCode { get; init; }
    public required string Display { get; init; }

    /// <summary>Same metric over the comparison window. Null when no comparison ran.</summary>
    public decimal? PreviousValue { get; init; }

    /// <summary>Pre-rendered previous value, so clients never re-implement formatting.</summary>
    public string? PreviousDisplay { get; init; }
}

public sealed class TableResult : ReportBlockResult
{
    public required IReadOnlyList<TableColumn> Columns { get; init; }
    public required IReadOnlyList<TableRow> Rows { get; init; }
    public TableRow? Totals { get; init; }
}

public sealed class TableColumn
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required TableColumnType ColumnType { get; init; }
    public string? CurrencyCode { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TableColumnType
{
    Text,
    Integer,
    Hours,
    Money,
    Percent,
    Date
}

public sealed class TableRow
{
    public required string Key { get; init; }
    public required IReadOnlyDictionary<string, TableCell> Cells { get; init; }

    /// <summary>Detail by default. A consumer that ignores this renders a flat table, same as before grouping existed.</summary>
    public TableRowKind Kind { get; init; } = TableRowKind.Detail;

    /// <summary>Nesting depth for multi-level grouping; 0 on an ungrouped table or a top-level group.</summary>
    public int Depth { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TableRowKind
{
    Detail,
    GroupHeader,
    GroupSubtotal
}

public sealed class TableCell
{
    public decimal? Number { get; init; }
    public required string Display { get; init; }

    /// <summary>Same cell over the comparison window, matched by row key. Null when absent.</summary>
    public decimal? PreviousNumber { get; init; }
}

public sealed class SeriesResult : ReportBlockResult
{
    public required ChartKind Kind { get; init; }
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<NamedSeries> Series { get; init; }
}

public sealed class NamedSeries
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<decimal> Values { get; init; }
}

public sealed class ProseResult : ReportBlockResult
{
    public required IReadOnlyList<string> Paragraphs { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetricUnit
{
    Hours,
    Money,
    Percent,
    Count,
    Rate
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetricScope
{
    Entry,
    Project,
    User
}

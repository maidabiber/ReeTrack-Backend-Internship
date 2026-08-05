using System.Text.Json.Serialization;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Models.CustomReports;

public sealed class CustomReportSpec
{
    public int Version { get; init; } = 1;
    public ReportQuery Query { get; init; } = new();
    public IReadOnlyList<ReportBlockSpec> Blocks { get; init; } = [];

    /// <summary>Baseline the report is measured against. Doubles the query cost when set.</summary>
    public ComparisonMode Comparison { get; init; } = ComparisonMode.None;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComparisonMode
{
    None,

    /// <summary>The equal-length window immediately before the report's own range.</summary>
    PreviousPeriod,

    /// <summary>The same range one year earlier — for seasonal work.</summary>
    SamePeriodLastYear
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KpiBlockSpec), "kpi")]
[JsonDerivedType(typeof(BreakdownBlockSpec), "breakdown")]
[JsonDerivedType(typeof(ChartBlockSpec), "chart")]
[JsonDerivedType(typeof(EntriesBlockSpec), "entries")]
[JsonDerivedType(typeof(NoteBlockSpec), "note")]
[JsonDerivedType(typeof(NarrativeBlockSpec), "narrative")]
public abstract class ReportBlockSpec
{
    public required string Id { get; init; }
    public string? Title { get; init; }
}

public sealed class KpiBlockSpec : ReportBlockSpec
{
    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public sealed class BreakdownBlockSpec : ReportBlockSpec
{
    public IReadOnlyList<string> Dimensions { get; init; } = [];
    public IReadOnlyList<string> Metrics { get; init; } = [];
    public IReadOnlyList<ComputedColumnSpec> Computed { get; init; } = [];
    public string? SortKey { get; init; }
    public bool SortDescending { get; init; } = true;
    public int? TopN { get; init; }
    public bool IncludeOthers { get; init; }
    public bool ShowTotals { get; init; } = true;
}

public sealed class ChartBlockSpec : ReportBlockSpec
{
    public required string Dimension { get; init; }
    public IReadOnlyList<string> Metrics { get; init; } = [];
    public ChartKind Kind { get; init; } = ChartKind.Bar;
    public int? TopN { get; init; }
}

public sealed class EntriesBlockSpec : ReportBlockSpec
{
    public IReadOnlyList<string> Columns { get; init; } = [];
    public IReadOnlyList<ReportGroupBy> GroupBy { get; init; } = [];
    public int Limit { get; init; } = 100;
}

public sealed class NoteBlockSpec : ReportBlockSpec
{
    public string Text { get; init; } = "";
}

public sealed class NarrativeBlockSpec : ReportBlockSpec
{
    /// <summary>What the reader cares about, steering which findings are surfaced.</summary>
    public string? Focus { get; init; }

    public string? CachedText { get; init; }
    public DateTime? GeneratedAtUtc { get; init; }

    /// <summary>
    /// Report fingerprint the cached text was written against. When it no longer matches the
    /// spec being run, the text describes different data and the block says so.
    /// </summary>
    public string? GeneratedForFingerprint { get; init; }
}

/// <param name="Right">Id of another metric on the same block. Null when <paramref name="RightValue"/> is used.</param>
/// <param name="RightValue">
/// Literal number for the right operand — "billable hours × 85" is the common case, and it
/// cannot be expressed with two metrics. Exactly one of Right / RightValue is set on the
/// arithmetic operators; PctOfTotal uses neither.
/// </param>
public sealed record ComputedColumnSpec(
    string Id,
    string Label,
    string Left,
    ComputedOperator Operator,
    string? Right = null,
    decimal? RightValue = null);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComputedOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    PctOfTotal
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChartKind
{
    Bar,
    Line,
    Area,
    Donut
}

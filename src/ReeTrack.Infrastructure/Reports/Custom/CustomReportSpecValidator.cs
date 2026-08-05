using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Infrastructure.Reports.Custom;

internal static class CustomReportSpecValidator
{
    public const int MaxBlocks = 12;
    public const int MaxMetricsPerBlock = 8;
    public const int MaxDimensionsPerBlock = 2;
    public const int MaxComputedPerBlock = 4;
    public const int MaxNoteLength = 2000;
    public const int MaxEntriesLimit = 1000;
    public const int MaxEntriesGroupBy = 2;
    public const int MaxNarrativeTextLength = 8000;
    public const int MaxNarrativeFocusLength = 200;
    public const decimal MaxComputedLiteral = 1_000_000_000m;

    public static void Validate(CustomReportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.Version != 1)
            throw AppErrors.Validation("Unsupported custom report schema version.");

        if (!Enum.IsDefined(spec.Comparison))
            throw AppErrors.Validation("The report uses an unsupported comparison mode.");

        if (spec.Blocks.Count == 0)
            throw AppErrors.Validation("A custom report needs at least one block.");

        if (spec.Blocks.Count > MaxBlocks)
            throw AppErrors.Validation($"A custom report can have at most {MaxBlocks} blocks.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in spec.Blocks)
        {
            if (string.IsNullOrWhiteSpace(block.Id))
                throw AppErrors.Validation("Every block needs an id.");

            if (!ids.Add(block.Id))
                throw AppErrors.Validation($"Duplicate block id '{block.Id}'.");

            switch (block)
            {
                case KpiBlockSpec kpi:
                    ValidateMetrics(kpi.Metrics, dimensions: []);
                    break;
                case BreakdownBlockSpec breakdown:
                    ValidateDimensions(breakdown.Dimensions);
                    ValidateMetrics(breakdown.Metrics, breakdown.Dimensions);
                    ValidateComputed(breakdown.Computed, breakdown.Metrics);
                    if (breakdown.TopN is <= 0)
                        throw AppErrors.Validation("breakdown.topN must be positive when set.");
                    ValidateSortKey(breakdown);
                    break;
                case ChartBlockSpec chart:
                    if (string.IsNullOrWhiteSpace(chart.Dimension))
                        throw AppErrors.Validation("A chart block needs a dimension.");
                    ValidateDimensions([chart.Dimension]);
                    if (chart.Metrics.Count is < 1 or > 3)
                        throw AppErrors.Validation("A chart block needs between 1 and 3 metrics.");
                    ValidateMetrics(chart.Metrics, [chart.Dimension]);
                    if (chart.TopN is <= 0)
                        throw AppErrors.Validation("chart.topN must be positive when set.");
                    break;
                case EntriesBlockSpec entries:
                    if (entries.Columns.Count == 0)
                        throw AppErrors.Validation("An entries block needs at least one column.");
                    foreach (var column in entries.Columns)
                    {
                        if (!EntryColumnCatalog.All.ContainsKey(column))
                            throw AppErrors.Validation($"Unknown entries column '{column}'.");
                    }
                    if (entries.Limit is < 1 or > MaxEntriesLimit)
                        throw AppErrors.Validation($"entries.limit must be between 1 and {MaxEntriesLimit}.");
                    if (entries.GroupBy.Any(group => !Enum.IsDefined(group)))
                        throw AppErrors.Validation("entries.groupBy contains an unsupported grouping.");
                    if (entries.GroupBy.Count > MaxEntriesGroupBy)
                        throw AppErrors.Validation($"entries.groupBy can have at most {MaxEntriesGroupBy} levels.");
                    break;
                case NoteBlockSpec note:
                    if (note.Text.Length > MaxNoteLength)
                        throw AppErrors.Validation($"A note block can be at most {MaxNoteLength} characters.");
                    break;
                case NarrativeBlockSpec narrative:
                    if (narrative.CachedText is { } cachedText && cachedText.Length > MaxNarrativeTextLength)
                        throw AppErrors.Validation($"Narrative text can be at most {MaxNarrativeTextLength} characters.");
                    if (narrative.Focus is { } focus && focus.Length > MaxNarrativeFocusLength)
                        throw AppErrors.Validation($"Narrative focus can be at most {MaxNarrativeFocusLength} characters.");
                    break;
                default:
                    throw AppErrors.Validation($"Unsupported block type '{block.GetType().Name}'.");
            }
        }
    }

    public static (bool NeedsCost, bool NeedsProjects, bool NeedsHourTargets) AnalyzeNeeds(CustomReportSpec spec)
    {
        var needsCost = false;
        var needsProjects = false;
        var needsHourTargets = false;

        foreach (var block in spec.Blocks)
        {
            IEnumerable<string> metrics = block switch
            {
                KpiBlockSpec kpi => kpi.Metrics,
                BreakdownBlockSpec breakdown => breakdown.Metrics,
                ChartBlockSpec chart => chart.Metrics,
                EntriesBlockSpec entries when entries.Columns.Any(c =>
                    c.Equals("labourCost", StringComparison.OrdinalIgnoreCase)) => ["labourCost"],
                _ => []
            };

            foreach (var metricId in metrics)
            {
                if (!MetricCatalog.All.TryGetValue(metricId, out var metric))
                    continue;
                needsCost |= metric.NeedsCost;
                needsProjects |= metric.NeedsProjects;
                needsHourTargets |= metric.NeedsHourTargets;
            }

            // hourType resolution uses EntryCostLine (holiday / OT buckets).
            IEnumerable<string> dimensions = block switch
            {
                BreakdownBlockSpec breakdown => breakdown.Dimensions,
                ChartBlockSpec chart => [chart.Dimension],
                _ => []
            };
            if (dimensions.Any(d => d.Equals("hourType", StringComparison.OrdinalIgnoreCase)))
                needsCost = true;
        }

        return (needsCost, needsProjects, needsHourTargets);
    }

    /// <summary>
    /// Only measured columns can order a breakdown — the evaluator looks the sort key up in the
    /// row's value map, so a dimension id silently sorts every row by 0.
    /// </summary>
    private static void ValidateSortKey(BreakdownBlockSpec breakdown)
    {
        if (breakdown.SortKey is not { } sortKey || string.IsNullOrWhiteSpace(sortKey))
            return;

        var sortable = breakdown.Metrics
            .Concat(breakdown.Computed.Select(column => column.Id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!sortable.Contains(sortKey))
        {
            throw AppErrors.Validation(
                $"breakdown.sortKey '{sortKey}' must be one of the block's metrics or computed columns.");
        }
    }

    private static void ValidateDimensions(IReadOnlyList<string> dimensions)
    {
        if (dimensions.Count is < 1 or > MaxDimensionsPerBlock)
            throw AppErrors.Validation($"A block can use between 1 and {MaxDimensionsPerBlock} dimensions.");

        foreach (var dimension in dimensions)
        {
            if (!DimensionCatalog.All.ContainsKey(dimension))
                throw AppErrors.Validation($"Unknown dimension '{dimension}'.");
        }
    }

    private static void ValidateMetrics(IReadOnlyList<string> metrics, IReadOnlyList<string> dimensions)
    {
        if (metrics.Count == 0)
            throw AppErrors.Validation("A block needs at least one metric.");

        if (metrics.Count > MaxMetricsPerBlock)
            throw AppErrors.Validation($"A block can use at most {MaxMetricsPerBlock} metrics.");

        foreach (var metricId in metrics)
        {
            var metric = MetricCatalog.GetRequired(metricId);
            if (!MetricCompatibility.IsValid(metric, dimensions))
            {
                throw AppErrors.Validation(
                    $"Metric '{metricId}' is not compatible with dimension(s) [{string.Join(", ", dimensions)}].");
            }
        }
    }

    private static void ValidateComputed(
        IReadOnlyList<ComputedColumnSpec> computed,
        IReadOnlyList<string> metrics)
    {
        if (computed.Count > MaxComputedPerBlock)
            throw AppErrors.Validation($"A breakdown can have at most {MaxComputedPerBlock} computed columns.");

        var known = new HashSet<string>(metrics, StringComparer.OrdinalIgnoreCase);
        var columnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in computed)
        {
            if (string.IsNullOrWhiteSpace(column.Id) || string.IsNullOrWhiteSpace(column.Label))
                throw AppErrors.Validation("Computed columns need an id and label.");

            // Row values are keyed by id — a computed column named after a metric or another
            // computed column would overwrite it instead of adding a column.
            if (known.Contains(column.Id))
                throw AppErrors.Validation(
                    $"Computed column id '{column.Id}' collides with the metric of the same name.");

            if (!columnIds.Add(column.Id))
                throw AppErrors.Validation($"Duplicate computed column id '{column.Id}'.");

            if (!known.Contains(column.Left))
                throw AppErrors.Validation($"Computed column '{column.Id}' references unknown left metric '{column.Left}'.");

            if (column.Operator is ComputedOperator.PctOfTotal)
            {
                if (column.Right is not null || column.RightValue is not null)
                    throw AppErrors.Validation($"Computed column '{column.Id}' must not set a right operand for {column.Operator}.");
                continue;
            }

            // The right operand is either another metric on the block or a literal number,
            // never both — otherwise the evaluator would have to guess which one wins.
            if (column.Right is not null && column.RightValue is not null)
            {
                throw AppErrors.Validation(
                    $"Computed column '{column.Id}' sets both a right metric and a right value.");
            }

            if (column.RightValue is { } literal)
            {
                // Keeps multiplication well away from decimal overflow on large metrics.
                if (Math.Abs(literal) > MaxComputedLiteral)
                    throw AppErrors.Validation(
                        $"Computed column '{column.Id}' right value must be between -{MaxComputedLiteral:N0} and {MaxComputedLiteral:N0}.");
                if (column.Operator is ComputedOperator.Divide && literal == 0m)
                    throw AppErrors.Validation($"Computed column '{column.Id}' cannot divide by zero.");
                continue;
            }

            if (column.Right is null || !known.Contains(column.Right))
                throw AppErrors.Validation($"Computed column '{column.Id}' references unknown right metric '{column.Right}'.");
        }
    }
}

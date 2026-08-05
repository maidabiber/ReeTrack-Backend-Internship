using System.Text;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Infrastructure.Reports.Custom.Insights;

/// <summary>
/// Turns an evaluated report into the two things the insight service needs: a compact text
/// digest for the prompt, and a lookup that resolves a model's reference back to a real figure.
/// </summary>
/// <remarks>
/// Only the aggregated IR is ever exposed. Individual time entries, their descriptions, and
/// anything else outside the rolled-up blocks stay out of the prompt.
/// </remarks>
internal sealed class InsightFacts
{
    /// <summary>Rows per table in the digest. Enough to see the shape without a huge prompt.</summary>
    private const int MaxRowsPerTable = 12;

    private readonly Dictionary<string, ReportBlockResult> _blocksById;

    private InsightFacts(
        Dictionary<string, ReportBlockResult> blocksById,
        string digest,
        bool hasComparison)
    {
        _blocksById = blocksById;
        Digest = digest;
        HasComparison = hasComparison;
    }

    /// <summary>Human-readable summary of the report, sent as the prompt's data section.</summary>
    public string Digest { get; }

    public bool HasComparison { get; }

    public static InsightFacts From(CustomReportDto report)
    {
        var builder = new StringBuilder();
        var blocksById = new Dictionary<string, ReportBlockResult>(StringComparer.Ordinal);

        builder.Append("Period: ").AppendLine(DescribePeriod(report));
        if (report.Comparison is { } comparison)
        {
            builder
                .Append("Comparison period: ")
                .Append(comparison.From.ToString("yyyy-MM-dd"))
                .Append(" to ")
                .AppendLine(comparison.To.ToString("yyyy-MM-dd"));
        }

        foreach (var block in report.Blocks)
        {
            blocksById[block.Id] = block;
            builder.AppendLine();
            builder.Append("BLOCK id=").Append(block.Id).Append(" title=").AppendLine(block.Title ?? "(untitled)");

            switch (block)
            {
                case KpiGroupResult kpi:
                    builder.AppendLine("type=kpi");
                    foreach (var cell in kpi.Cells)
                    {
                        builder
                            .Append("  column=").Append(cell.Key)
                            .Append(" label=").Append(cell.Label)
                            .Append(" value=").Append(cell.Display);
                        if (cell.PreviousDisplay is { } previous)
                            builder.Append(" previous=").Append(previous);
                        builder.AppendLine();
                    }
                    break;

                case TableResult table:
                    builder.AppendLine("type=table");
                    builder
                        .Append("  columns=")
                        .AppendLine(string.Join(", ", table.Columns.Select(c => $"{c.Key} ({c.Label})")));
                    foreach (var row in table.Rows.Take(MaxRowsPerTable))
                        AppendRow(builder, table, row);
                    if (table.Rows.Count > MaxRowsPerTable)
                        builder.Append("  (").Append(table.Rows.Count - MaxRowsPerTable).AppendLine(" further rows omitted)");
                    break;

                case SeriesResult series:
                    builder.AppendLine("type=chart");
                    foreach (var line in series.Series)
                    {
                        builder
                            .Append("  series=").Append(line.Label)
                            .Append(" points=")
                            .AppendLine(string.Join(", ", series.Categories
                                .Zip(line.Values, (category, value) => $"{category}:{value}")));
                    }
                    break;

                case ProseResult:
                    // Existing commentary is not evidence; leave it out so the model cannot
                    // build on its own earlier text.
                    builder.AppendLine("type=prose (omitted)");
                    break;
            }
        }

        return new InsightFacts(blocksById, builder.ToString(), report.Comparison is not null);
    }

    private static void AppendRow(StringBuilder builder, TableResult table, TableRow row)
    {
        builder.Append("  row=").Append(row.Key);
        foreach (var column in table.Columns)
        {
            if (!row.Cells.TryGetValue(column.Key, out var cell))
                continue;

            builder.Append(' ').Append(column.Key).Append('=').Append(cell.Display);
            if (cell.PreviousNumber is { } previous)
                builder.Append("(prev ").Append(previous).Append(')');
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Renders the figure a finding points at. Returns null when the reference does not resolve,
    /// which is how a hallucinated block, row, or column gets dropped instead of printed.
    /// </summary>
    public string? ResolveReference(string? blockId, string? rowKey, string? columnKey)
    {
        if (string.IsNullOrWhiteSpace(blockId)
            || string.IsNullOrWhiteSpace(columnKey)
            || !_blocksById.TryGetValue(blockId, out var block))
        {
            return null;
        }

        return block switch
        {
            KpiGroupResult kpi => ResolveKpi(kpi, columnKey),
            TableResult table => ResolveTable(table, rowKey, columnKey),
            _ => null
        };
    }

    private static string? ResolveKpi(KpiGroupResult kpi, string columnKey)
    {
        var cell = kpi.Cells.FirstOrDefault(c => string.Equals(c.Key, columnKey, StringComparison.OrdinalIgnoreCase));
        if (cell is null)
            return null;

        return cell.PreviousDisplay is { } previous
            ? $"{cell.Label}: {cell.Display} (was {previous})"
            : $"{cell.Label}: {cell.Display}";
    }

    private static string? ResolveTable(TableResult table, string? rowKey, string columnKey)
    {
        if (string.IsNullOrWhiteSpace(rowKey))
            return null;

        var row = table.Rows.FirstOrDefault(r => string.Equals(r.Key, rowKey, StringComparison.Ordinal));
        var column = table.Columns.FirstOrDefault(c => string.Equals(c.Key, columnKey, StringComparison.OrdinalIgnoreCase));
        if (row is null || column is null || !row.Cells.TryGetValue(column.Key, out var cell))
            return null;

        // Prefer the row's own leading label over its opaque key.
        var rowLabel = table.Columns
            .Select(c => row.Cells.GetValueOrDefault(c.Key))
            .FirstOrDefault(c => c is not null && !string.IsNullOrWhiteSpace(c.Display))
            ?.Display ?? rowKey;

        var previous = cell.PreviousNumber is { } number ? $", was {number:0.##}" : "";
        return $"{rowLabel} — {column.Label}: {cell.Display}{previous}";
    }

    private static string DescribePeriod(CustomReportDto report) =>
        (report.FilterFromDate, report.FilterToDate) switch
        {
            ({ } from, { } to) => $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
            ({ } from, null) => $"from {from:yyyy-MM-dd}",
            (null, { } to) => $"up to {to:yyyy-MM-dd}",
            _ => "all time"
        };
}

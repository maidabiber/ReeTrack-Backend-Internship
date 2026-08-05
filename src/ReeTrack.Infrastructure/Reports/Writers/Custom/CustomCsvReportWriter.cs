using System.Text;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Infrastructure.Reports.Writers.Custom;

public sealed class CustomCsvReportWriter : IReportWriter<CustomReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Csv;

    public ReportFile Write(CustomReportDto model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section,Key,Value");
        CsvWriterSupport.AppendOverview(sb, "TotalHours", ReportFormat.HoursLabel(model.Kpis.TotalSeconds));
        CsvWriterSupport.AppendOverview(sb, "BillableHours", ReportFormat.HoursLabel(model.Kpis.BillableSeconds));
        CsvWriterSupport.AppendOverview(sb, "EntryCount", model.Kpis.EntryCount);
        CsvWriterSupport.AppendOverview(sb, "Period", ReportFormat.PeriodLabel(model));
        CsvWriterSupport.AppendOverview(sb, "GeneratedAtUtc", ReportFormat.FriendlyDateTime(model.GeneratedAtUtc));
        if (!string.IsNullOrWhiteSpace(model.GeneratedByName))
            CsvWriterSupport.AppendOverview(sb, "GeneratedBy", model.GeneratedByName);
        if (model.Comparison is { } comparison)
        {
            CsvWriterSupport.AppendOverview(
                sb, "ComparisonPeriod", $"{comparison.From:yyyy-MM-dd} to {comparison.To:yyyy-MM-dd}");
        }
        foreach (var warning in model.Warnings)
            CsvWriterSupport.AppendOverview(sb, "Warning", warning);
        foreach (var line in ReportFormat.CustomBasisLines(model.Basis))
            CsvWriterSupport.AppendOverview(sb, "Basis", line);

        foreach (var block in model.Blocks)
        {
            sb.AppendLine();
            sb.Append("Section,").AppendLine(CsvWriterSupport.Escape(block.Title ?? block.Id));
            switch (block)
            {
                case KpiGroupResult kpi:
                    sb.AppendLine("Key,Value");
                    foreach (var cell in kpi.Cells)
                    {
                        sb.Append(CsvWriterSupport.Escape(cell.Label)).Append(',');
                        var value = cell.Value is { } number
                            ? CsvWriterSupport.FormatDecimal(number)
                            : cell.Display;
                        var suffix = cell.PreviousDisplay is { } previous ? $" (was {previous})" : "";
                        sb.AppendLine(CsvWriterSupport.Escape(value + suffix));
                    }
                    break;

                case TableResult table:
                    sb.AppendLine(string.Join(',', table.Columns.Select(c => CsvWriterSupport.Escape(c.Label))));
                    foreach (var row in table.Rows)
                        AppendTableRow(sb, table.Columns, row);
                    if (table.Totals is { } totals)
                        AppendTableRow(sb, table.Columns, totals);
                    break;

                case SeriesResult series:
                    sb.Append(CsvWriterSupport.Escape("Category"));
                    foreach (var s in series.Series)
                        sb.Append(',').Append(CsvWriterSupport.Escape(s.Label));
                    sb.AppendLine();
                    for (var i = 0; i < series.Categories.Count; i++)
                    {
                        sb.Append(CsvWriterSupport.Escape(series.Categories[i]));
                        foreach (var s in series.Series)
                        {
                            var value = i < s.Values.Count ? s.Values[i] : 0m;
                            sb.Append(',').Append(CsvWriterSupport.FormatDecimal(value));
                        }
                        sb.AppendLine();
                    }
                    break;

                case ProseResult prose:
                    foreach (var paragraph in prose.Paragraphs)
                        sb.AppendLine(CsvWriterSupport.Escape(paragraph));
                    break;
            }

            if (!string.IsNullOrWhiteSpace(block.Footnote))
                sb.Append("Footnote,").AppendLine(CsvWriterSupport.Escape(block.Footnote));
        }

        return new ReportFile(
            CsvWriterSupport.ToUtf8BytesWithBom(sb),
            "text/csv",
            ReportFileNames.ForCustom(ReportExportFormat.Csv, model.GeneratedAtUtc));
    }

    private static void AppendTableRow(
        StringBuilder sb,
        IReadOnlyList<TableColumn> columns,
        TableRow row)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (i > 0)
                sb.Append(',');

            var column = columns[i];
            var cell = row.Cells.GetValueOrDefault(column.Key);
            var value = cell?.Number is { } number
                && column.ColumnType is TableColumnType.Hours or TableColumnType.Money
                    or TableColumnType.Percent or TableColumnType.Integer
                ? CsvWriterSupport.FormatDecimal(number)
                : cell?.Display ?? "";
            var suffix = ReportFormat.PreviousCellSuffix(cell?.PreviousNumber, column.ColumnType, column.CurrencyCode);
            sb.Append(CsvWriterSupport.Escape(value + suffix));
        }

        sb.AppendLine();
    }
}

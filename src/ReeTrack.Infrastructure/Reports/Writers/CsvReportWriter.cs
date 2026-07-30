using System.Globalization;
using System.Text;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class CsvReportWriter : IReportWriter<SummaryReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Csv;

    public ReportFile Write(SummaryReportDto model)
    {
        var sb = new StringBuilder();
        var total = model.Kpis.TotalSeconds;

        sb.AppendLine("Section,Key,Value");
        AppendKpi(sb, "Highlights", ReportFormat.Highlights(model));
        AppendKpi(sb, "TotalHours", ReportFormat.HoursLabel(model.Kpis.TotalSeconds));
        AppendKpi(sb, "BillableHours", ReportFormat.HoursLabel(model.Kpis.BillableSeconds));
        AppendKpi(sb, "NonBillableHours", ReportFormat.HoursLabel(model.Kpis.NonBillableSeconds));
        AppendKpi(sb, "BillablePct", ReportFormat.Percent(model.Kpis.BillablePct));
        AppendKpi(sb, "EntryCount", model.Kpis.EntryCount);
        AppendKpi(sb, "ActiveMembers", model.Kpis.ActiveMembers);
        AppendKpi(sb, "ActiveProjects", model.Kpis.ActiveProjects);
        AppendKpi(sb, "OvertimeHours", ReportFormat.HoursLabel(model.Kpis.OvertimeHours));
        AppendKpi(sb, "WeekendHours", ReportFormat.HoursLabel(model.Kpis.WeekendHours));
        AppendKpi(sb, "HolidayHours", ReportFormat.HoursLabel(model.Kpis.HolidayHours));
        AppendKpi(sb, "UnassignedHours", ReportFormat.HoursLabel(model.Kpis.UnassignedSeconds));
        AppendKpi(sb, "Period", ReportFormat.PeriodLabel(model));
        AppendKpi(sb, "GeneratedAtUtc", ReportFormat.FriendlyDateTime(model.GeneratedAtUtc));
        if (!string.IsNullOrWhiteSpace(model.GeneratedByName))
            AppendKpi(sb, "GeneratedBy", model.GeneratedByName);
        foreach (var line in ReportFormat.SummaryBasisLines(model))
            AppendKpi(sb, "Basis", line);
        sb.AppendLine();

        sb.AppendLine("DayOfWeek,Hours,HoursDecimal,PctOfTotal");
        foreach (var day in model.Activity)
        {
            sb.Append(CsvWriterSupport.Escape(day.DayOfWeek)).Append(',');
            sb.Append(CsvWriterSupport.Escape(ReportFormat.HoursLabel(day.TotalSeconds))).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(ReportFormat.Hours(day.TotalSeconds))).Append(',');
            sb.AppendLine(CsvWriterSupport.Escape(ReportFormat.Percent(SummaryReportAnalytics.PctOfTotal(day.TotalSeconds, total))));
        }

        sb.AppendLine();
        // ISO week-start dates: sortable, unambiguous across a year boundary, and
        // parsed as a real date by every importer. The friendly label is PDF-only.
        sb.AppendLine("WeekStart,Hours,HoursDecimal");
        foreach (var week in model.WeeklyTrend)
        {
            sb.Append(CsvWriterSupport.Escape(ReportFormat.IsoDate(week.WeekStartDate))).Append(',');
            sb.Append(CsvWriterSupport.Escape(ReportFormat.HoursLabel(week.TotalSeconds))).Append(',');
            sb.AppendLine(CsvWriterSupport.FormatDecimal(ReportFormat.Hours(week.TotalSeconds)));
        }

        sb.AppendLine();
        // Money is carried as raw decimals with a separate CurrencyCode column. CSV is the
        // machine-readable export; "1,234.56 EUR" is a label, not a number you can sum.
        string[] projectColumns =
        [
            "Name", "Client", "Status", "CurrencyCode",
            "Hours", "HoursDecimal", "PctOfTotal",
            "EstimateHours", "EstimateUsedPct", "BillingModel",
            "HourlyRate", "FixedFee", "Cost", "Margin",
            "NormalCost", "WeekendCost", "HolidayCost", "OvertimeCost",
            "OvertimeHours", "WeekendHours", "HolidayHours"
        ];
        AppendRow(sb, projectColumns);

        foreach (var project in model.Projects)
        {
            var estimateUsed = SummaryReportAnalytics.EstimateUsedPct(project.TotalSeconds, project.TimeEstimateHours);
            var margin = SummaryReportAnalytics.FixedFeeMargin(project.FixedFeeAmount, project.CalculatedCost);

            AppendRow(sb, [
                project.Name,
                project.ClientName,
                project.Status,
                project.CurrencyCode,
                ReportFormat.HoursLabel(project.TotalSeconds),
                CsvWriterSupport.FormatDecimal(ReportFormat.Hours(project.TotalSeconds)),
                CsvWriterSupport.FormatDecimal(SummaryReportAnalytics.PctOfTotal(project.TotalSeconds, total)),
                Optional(project.TimeEstimateHours),
                Optional(estimateUsed),
                // Enum name, not the "—" display label: this column gets parsed, not read.
                SummaryReportAnalytics.BillingModel(project.HourlyRate, project.FixedFeeAmount).ToString(),
                Optional(project.HourlyRate),
                Optional(project.FixedFeeAmount),
                CsvWriterSupport.FormatDecimal(project.CalculatedCost),
                Optional(margin),
                CsvWriterSupport.FormatDecimal(project.NormalCost),
                CsvWriterSupport.FormatDecimal(project.WeekendCost),
                CsvWriterSupport.FormatDecimal(project.HolidayCost),
                CsvWriterSupport.FormatDecimal(project.OvertimeCost),
                CsvWriterSupport.FormatDecimal(project.OvertimeHours),
                CsvWriterSupport.FormatDecimal(project.WeekendHours),
                CsvWriterSupport.FormatDecimal(project.HolidayHours)
            ]);
        }

        // Time logged against no project. Without this row the project rows do not
        // reconcile to TotalHours and PctOfTotal never reaches 100%.
        if (model.Kpis.UnassignedSeconds > 0)
        {
            var unassigned = model.Kpis.UnassignedSeconds;
            var unassignedRow = new string[projectColumns.Length];
            Array.Fill(unassignedRow, string.Empty);
            unassignedRow[0] = ReportFormat.UnassignedLabel;
            unassignedRow[4] = ReportFormat.HoursLabel(unassigned);
            unassignedRow[5] = CsvWriterSupport.FormatDecimal(ReportFormat.Hours(unassigned));
            unassignedRow[6] = CsvWriterSupport.FormatDecimal(SummaryReportAnalytics.PctOfTotal(unassigned, total));
            AppendRow(sb, unassignedRow);
        }

        sb.AppendLine();
        sb.AppendLine("DisplayName,Hours,HoursDecimal,PctOfTotal");
        foreach (var member in model.Members)
        {
            sb.Append(CsvWriterSupport.Escape(member.DisplayName)).Append(',');
            sb.Append(CsvWriterSupport.Escape(ReportFormat.HoursLabel(member.TotalSeconds))).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(ReportFormat.Hours(member.TotalSeconds))).Append(',');
            sb.AppendLine(CsvWriterSupport.Escape(ReportFormat.Percent(SummaryReportAnalytics.PctOfTotal(member.TotalSeconds, total))));
        }

        return new ReportFile(
            CsvWriterSupport.ToUtf8BytesWithBom(sb),
            "text/csv",
            ReportFileNames.For(ReportExportFormat.Csv, model.GeneratedAtUtc));
    }

    private static void AppendKpi(StringBuilder sb, string key, object value) =>
        sb.Append("Summary,").Append(CsvWriterSupport.Escape(key)).Append(',')
            .AppendLine(CsvWriterSupport.Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty));

    /// <summary>Writes one escaped, comma-separated row — the column count can't drift.</summary>
    private static void AppendRow(StringBuilder sb, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(CsvWriterSupport.Escape(fields[i]));
        }
        sb.AppendLine();
    }

    /// <summary>Empty cell for an unset optional number, rather than a placeholder glyph.</summary>
    private static string Optional(decimal? value) =>
        value is { } present ? CsvWriterSupport.FormatDecimal(present) : string.Empty;
}

using System.Text;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class CsvDetailedReportWriter : IReportWriter<DetailedReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Csv;

    public ReportFile Write(DetailedReportDto model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section,Key,Value");
        CsvWriterSupport.AppendOverview(sb, "TotalHours", ReportFormat.HoursLabel(model.Kpis.TotalSeconds));
        CsvWriterSupport.AppendOverview(sb, "BillableHours", ReportFormat.HoursLabel(model.Kpis.BillableSeconds));
        CsvWriterSupport.AppendOverview(sb, "NonBillableHours", ReportFormat.HoursLabel(model.Kpis.NonBillableSeconds));
        CsvWriterSupport.AppendOverview(sb, "BillablePct", ReportFormat.Percent(model.Kpis.BillablePct));
        CsvWriterSupport.AppendOverview(sb, "EntryCount", model.Kpis.EntryCount);
        CsvWriterSupport.AppendOverview(sb, "ActiveMembers", model.Kpis.ActiveMembers);
        CsvWriterSupport.AppendOverview(sb, "ActiveProjects", model.Kpis.ActiveProjects);
        CsvWriterSupport.AppendOverview(sb, "OvertimeHours", ReportFormat.HoursLabel(model.Kpis.OvertimeHours));
        CsvWriterSupport.AppendOverview(sb, "WeekendHours", ReportFormat.HoursLabel(model.Kpis.WeekendHours));
        CsvWriterSupport.AppendOverview(sb, "HolidayHours", ReportFormat.HoursLabel(model.Kpis.HolidayHours));
        CsvWriterSupport.AppendOverview(sb, "UnassignedHours", ReportFormat.HoursLabel(model.Kpis.UnassignedSeconds));
        CsvWriterSupport.AppendOverview(sb, "Period", ReportFormat.PeriodLabel(model));
        CsvWriterSupport.AppendOverview(sb, "GeneratedAtUtc", ReportFormat.FriendlyDateTime(model.GeneratedAtUtc));
        if (!string.IsNullOrWhiteSpace(model.GeneratedByName))
            CsvWriterSupport.AppendOverview(sb, "GeneratedBy", model.GeneratedByName);
        foreach (var line in ReportFormat.DetailedBasisLines(model))
            CsvWriterSupport.AppendOverview(sb, "Basis", line);
        sb.AppendLine();

        sb.AppendLine(
            "Date,Member,Client,Project,Task,Tags,Description,Billable,Hours,HoursDecimal," +
            "Cost,NormalCost,WeekendCost,HolidayCost,OvertimeCost,Currency," +
            "WeekendHours,HolidayHours,OvertimeHours,IsWeekend,IsHoliday,EntryId");

        foreach (var (group, entries) in DetailedReportExportRows.Enumerate(model))
        {
            if (group is not null)
            {
                // Marker row matching the UI group header (label · count · hours).
                sb.Append(CsvWriterSupport.Escape("Group")).Append(',');
                sb.Append(CsvWriterSupport.Escape(DetailedReportExportRows.GroupSummary(group)));
                for (var i = 0; i < 20; i++)
                    sb.Append(',');
                sb.AppendLine();
            }

            foreach (var entry in entries)
                WriteEntry(sb, entry);
        }

        return new ReportFile(
            CsvWriterSupport.ToUtf8BytesWithBom(sb),
            "text/csv",
            ReportFileNames.ForDetailed(ReportExportFormat.Csv, model.GeneratedAtUtc));
    }

    private static void WriteEntry(StringBuilder sb, DetailedEntryDto entry)
    {
        sb.Append(CsvWriterSupport.Escape(ReportFormat.IsoDate(entry.EntryDate))).Append(',');
        sb.Append(CsvWriterSupport.Escape(entry.DisplayName)).Append(',');
        sb.Append(CsvWriterSupport.Escape(entry.ClientName ?? "")).Append(',');
        sb.Append(CsvWriterSupport.Escape(entry.ProjectName ?? ReportFormat.UnassignedLabel)).Append(',');
        sb.Append(CsvWriterSupport.Escape(entry.TaskName ?? "")).Append(',');
        sb.Append(CsvWriterSupport.Escape(string.Join("; ", entry.Tags))).Append(',');
        sb.Append(CsvWriterSupport.Escape(entry.Description ?? "")).Append(',');
        sb.Append(entry.IsBillable ? "Yes" : "No").Append(',');
        sb.Append(CsvWriterSupport.Escape(ReportFormat.HoursLabel(entry.DurationSeconds))).Append(',');
        sb.Append(CsvWriterSupport.FormatDecimal(ReportFormat.Hours(entry.DurationSeconds))).Append(',');
        sb.Append(CsvWriterSupport.FormatDecimal(entry.CalculatedCost)).Append(',');
        sb.Append(CsvWriterSupport.FormatDecimal(entry.NormalCost)).Append(',');
        sb.Append(CsvWriterSupport.FormatDecimal(entry.WeekendCost)).Append(',');
        sb.Append(CsvWriterSupport.FormatDecimal(entry.HolidayCost)).Append(',');
        sb.Append(CsvWriterSupport.FormatDecimal(entry.OvertimeCost)).Append(',');
        sb.Append(CsvWriterSupport.Escape(entry.CurrencyCode ?? "")).Append(',');
        sb.Append(CsvWriterSupport.FormatDecimal(entry.WeekendHours)).Append(',');
        sb.Append(CsvWriterSupport.FormatDecimal(entry.HolidayHours)).Append(',');
        sb.Append(CsvWriterSupport.FormatDecimal(entry.OvertimeHours)).Append(',');
        sb.Append(entry.IsWeekend ? "Yes" : "No").Append(',');
        sb.Append(entry.IsHoliday ? "Yes" : "No").Append(',');
        sb.AppendLine(entry.EntryId.ToString());
    }
}

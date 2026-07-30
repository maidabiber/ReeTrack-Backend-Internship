using System.Globalization;
using System.Text;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class CsvDetailedReportWriter : IDetailedReportWriter
{
    public ReportExportFormat Format => ReportExportFormat.Csv;

    public ReportFile Write(DetailedReportDto model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section,Key,Value");
        Append(sb, "TotalHours", ReportFormat.HoursLabel(model.Kpis.TotalSeconds));
        Append(sb, "BillableHours", ReportFormat.HoursLabel(model.Kpis.BillableSeconds));
        Append(sb, "NonBillableHours", ReportFormat.HoursLabel(model.Kpis.NonBillableSeconds));
        Append(sb, "BillablePct", ReportFormat.Percent(model.Kpis.BillablePct));
        Append(sb, "EntryCount", model.Kpis.EntryCount);
        Append(sb, "ActiveMembers", model.Kpis.ActiveMembers);
        Append(sb, "ActiveProjects", model.Kpis.ActiveProjects);
        Append(sb, "OvertimeHours", ReportFormat.HoursLabel(model.Kpis.OvertimeHours));
        Append(sb, "WeekendHours", ReportFormat.HoursLabel(model.Kpis.WeekendHours));
        Append(sb, "HolidayHours", ReportFormat.HoursLabel(model.Kpis.HolidayHours));
        Append(sb, "UnassignedHours", ReportFormat.HoursLabel(model.Kpis.UnassignedSeconds));
        Append(sb, "Period", ReportFormat.PeriodLabel(model));
        Append(sb, "GeneratedAtUtc", ReportFormat.FriendlyDateTime(model.GeneratedAtUtc));
        if (!string.IsNullOrWhiteSpace(model.GeneratedByName))
            Append(sb, "GeneratedBy", model.GeneratedByName);
        foreach (var line in ReportFormat.BasisLines(model))
            Append(sb, "Basis", line);
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
                sb.Append(Escape("Group")).Append(',');
                sb.Append(Escape(DetailedReportExportRows.GroupSummary(group)));
                for (var i = 0; i < 20; i++)
                    sb.Append(',');
                sb.AppendLine();
            }

            foreach (var entry in entries)
                WriteEntry(sb, entry);
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

        return new ReportFile(
            bytes,
            "text/csv",
            ReportFileNames.ForDetailed(ReportExportFormat.Csv, model.GeneratedAtUtc));
    }

    private static void WriteEntry(StringBuilder sb, DetailedEntryDto entry)
    {
        sb.Append(Escape(ReportFormat.IsoDate(entry.EntryDate))).Append(',');
        sb.Append(Escape(entry.DisplayName)).Append(',');
        sb.Append(Escape(entry.ClientName ?? "")).Append(',');
        sb.Append(Escape(entry.ProjectName ?? ReportFormat.UnassignedLabel)).Append(',');
        sb.Append(Escape(entry.TaskName ?? "")).Append(',');
        sb.Append(Escape(string.Join("; ", entry.Tags))).Append(',');
        sb.Append(Escape(entry.Description ?? "")).Append(',');
        sb.Append(entry.IsBillable ? "Yes" : "No").Append(',');
        sb.Append(Escape(ReportFormat.HoursLabel(entry.DurationSeconds))).Append(',');
        sb.Append(FormatDecimal(ReportFormat.Hours(entry.DurationSeconds))).Append(',');
        sb.Append(FormatDecimal(entry.CalculatedCost)).Append(',');
        sb.Append(FormatDecimal(entry.NormalCost)).Append(',');
        sb.Append(FormatDecimal(entry.WeekendCost)).Append(',');
        sb.Append(FormatDecimal(entry.HolidayCost)).Append(',');
        sb.Append(FormatDecimal(entry.OvertimeCost)).Append(',');
        sb.Append(Escape(entry.CurrencyCode ?? "")).Append(',');
        sb.Append(FormatDecimal(entry.WeekendHours)).Append(',');
        sb.Append(FormatDecimal(entry.HolidayHours)).Append(',');
        sb.Append(FormatDecimal(entry.OvertimeHours)).Append(',');
        sb.Append(entry.IsWeekend ? "Yes" : "No").Append(',');
        sb.Append(entry.IsHoliday ? "Yes" : "No").Append(',');
        sb.AppendLine(entry.EntryId.ToString());
    }

    private static void Append(StringBuilder sb, string key, object value)
    {
        sb.Append("Overview,").Append(Escape(key)).Append(',').AppendLine(Escape(value?.ToString() ?? ""));
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

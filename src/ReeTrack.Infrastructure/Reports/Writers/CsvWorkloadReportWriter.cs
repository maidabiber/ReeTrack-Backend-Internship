using System.Globalization;
using System.Text;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class CsvWorkloadReportWriter : IReportWriter<WorkloadReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Csv;

    public ReportFile Write(WorkloadReportDto model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section,Key,Value");
        CsvWriterSupport.AppendOverview(sb, "TotalHours", ReportFormat.HoursLabel(model.Kpis.TotalSeconds));
        CsvWriterSupport.AppendOverview(sb, "BillableHours", ReportFormat.HoursLabel(model.Kpis.BillableSeconds));
        CsvWriterSupport.AppendOverview(sb, "ActiveMembers", model.Kpis.ActiveMembers);
        CsvWriterSupport.AppendOverview(sb, "ActiveProjects", model.Kpis.ActiveProjects);
        CsvWriterSupport.AppendOverview(sb, "Period", ReportFormat.PeriodLabel(model));
        CsvWriterSupport.AppendOverview(sb, "GeneratedAtUtc", ReportFormat.FriendlyDateTime(model.GeneratedAtUtc));
        if (!string.IsNullOrWhiteSpace(model.GeneratedByName))
            CsvWriterSupport.AppendOverview(sb, "GeneratedBy", model.GeneratedByName);
        foreach (var line in ReportFormat.WorkloadBasisLines(model.Basis))
            CsvWriterSupport.AppendOverview(sb, "Basis", line);
        sb.AppendLine();

        sb.AppendLine("Member,Client,Project,Hours,BillableHours,PctOfMember");
        foreach (var row in model.Allocations)
        {
            sb.Append(CsvWriterSupport.Escape(row.DisplayName)).Append(',');
            sb.Append(CsvWriterSupport.Escape(row.ClientName)).Append(',');
            sb.Append(CsvWriterSupport.Escape(row.ProjectName)).Append(',');
            sb.Append(FormatHours(row.TotalSeconds)).Append(',');
            sb.Append(FormatHours(row.BillableSeconds)).Append(',');
            sb.AppendLine(CsvWriterSupport.FormatDecimal(row.PctOfMemberTotal));
        }

        sb.Append(CsvWriterSupport.Escape("Total")).Append(',');
        sb.Append(',').Append(',');
        sb.Append(FormatHours(model.GrandTotalSeconds)).Append(',');
        sb.Append(FormatHours(model.GrandTotalBillableSeconds)).Append(',');
        sb.AppendLine();

        if (model.Schedule.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Schedule,Label,Hours,PctOfTotal");
            foreach (var item in model.Schedule)
            {
                sb.Append("Schedule,");
                sb.Append(CsvWriterSupport.Escape(item.Label)).Append(',');
                sb.Append(item.Hours.ToString("0.##", CultureInfo.InvariantCulture)).Append(',');
                sb.AppendLine(CsvWriterSupport.FormatDecimal(item.PctOfTotalHours));
            }
        }

        return new ReportFile(
            CsvWriterSupport.ToUtf8BytesWithBom(sb),
            "text/csv",
            ReportFileNames.ForWorkload(ReportExportFormat.Csv, model.GeneratedAtUtc));
    }

    private static string FormatHours(long seconds) =>
        CsvWriterSupport.FormatDecimal(ReportFormat.Hours(seconds));
}

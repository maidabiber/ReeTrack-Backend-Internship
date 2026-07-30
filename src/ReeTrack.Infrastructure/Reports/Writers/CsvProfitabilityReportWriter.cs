using System.Globalization;
using System.Text;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class CsvProfitabilityReportWriter : IReportWriter<ProfitabilityReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Csv;

    public ReportFile Write(ProfitabilityReportDto model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section,Key,Value");
        CsvWriterSupport.AppendOverview(sb, "TotalHours", ReportFormat.HoursLabel(model.Kpis.TotalSeconds));
        CsvWriterSupport.AppendOverview(sb, "BillableHours", ReportFormat.HoursLabel(model.Kpis.BillableSeconds));
        CsvWriterSupport.AppendOverview(sb, "Period", ReportFormat.PeriodLabel(model));
        CsvWriterSupport.AppendOverview(sb, "GeneratedAtUtc", ReportFormat.FriendlyDateTime(model.GeneratedAtUtc));
        if (!string.IsNullOrWhiteSpace(model.GeneratedByName))
            CsvWriterSupport.AppendOverview(sb, "GeneratedBy", model.GeneratedByName);
        foreach (var line in ReportFormat.ProfitabilityBasisLines(model.Basis))
            CsvWriterSupport.AppendOverview(sb, "Basis", line);
        sb.AppendLine();

        sb.AppendLine("Currency,Revenue,Cost,Margin,MarginPct,BillableHours,Projects");
        foreach (var currency in model.ByCurrency)
        {
            sb.Append(CsvWriterSupport.Escape(currency.CurrencyCode)).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(currency.Revenue)).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(currency.Cost)).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(currency.Margin)).Append(',');
            sb.Append(currency.MarginPct is { } pct ? CsvWriterSupport.FormatDecimal(pct) : "").Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(currency.BillableHours)).Append(',');
            sb.AppendLine(currency.ProjectCount.ToString(CultureInfo.InvariantCulture));
        }
        sb.AppendLine();

        sb.AppendLine("WeekStart,Currency,Revenue,Cost,Margin");
        foreach (var week in model.WeeklyTrend)
        {
            sb.Append(week.WeekStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(CsvWriterSupport.Escape(week.CurrencyCode)).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(week.Revenue)).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(week.Cost)).Append(',');
            sb.AppendLine(CsvWriterSupport.FormatDecimal(week.Margin));
        }
        sb.AppendLine();

        sb.AppendLine("Project,Client,Currency,BillingModel,Hours,BillableHours,Revenue,Cost,Margin,MarginPct,EstimateUsedPct");
        foreach (var project in model.Projects)
        {
            sb.Append(CsvWriterSupport.Escape(project.Name)).Append(',');
            sb.Append(CsvWriterSupport.Escape(project.ClientName)).Append(',');
            sb.Append(CsvWriterSupport.Escape(project.CurrencyCode)).Append(',');
            sb.Append(CsvWriterSupport.Escape(project.BillingModel)).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(ReportFormat.Hours(project.TotalSeconds))).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(ReportFormat.Hours(project.BillableSeconds))).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(project.Revenue)).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(project.CalculatedCost)).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(project.Margin)).Append(',');
            sb.Append(project.MarginPct is { } pct ? CsvWriterSupport.FormatDecimal(pct) : "").Append(',');
            sb.AppendLine(project.EstimateUsedPct is { } est ? CsvWriterSupport.FormatDecimal(est) : "");
        }
        sb.AppendLine();

        sb.AppendLine("Member,Currency,Hours,LabourCost");
        foreach (var member in model.Members)
        {
            sb.Append(CsvWriterSupport.Escape(member.DisplayName)).Append(',');
            sb.Append(CsvWriterSupport.Escape(member.CurrencyCode)).Append(',');
            sb.Append(CsvWriterSupport.FormatDecimal(ReportFormat.Hours(member.TotalSeconds))).Append(',');
            sb.AppendLine(CsvWriterSupport.FormatDecimal(member.LabourCost));
        }

        return new ReportFile(
            CsvWriterSupport.ToUtf8BytesWithBom(sb),
            "text/csv",
            ReportFileNames.ForProfitability(ReportExportFormat.Csv, model.GeneratedAtUtc));
    }
}

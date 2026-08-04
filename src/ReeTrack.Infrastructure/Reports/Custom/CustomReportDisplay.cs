using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Application.Reports;
using ReeTrack.Infrastructure.Reports.Writers;

namespace ReeTrack.Infrastructure.Reports.Custom;

internal static class CustomReportDisplay
{
    public static string Format(decimal? value, MetricUnit unit, string? currencyCode)
    {
        if (value is null)
            return "—";

        return unit switch
        {
            MetricUnit.Hours => ReportFormat.Hours2(value.Value) + "h",
            MetricUnit.Money => ReportFormat.Money(
                value.Value,
                string.IsNullOrWhiteSpace(currencyCode)
                    ? SummaryReportAnalytics.NoCurrencyCode
                    : currencyCode),
            MetricUnit.Percent => ReportFormat.Percent(value.Value),
            MetricUnit.Count => value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            MetricUnit.Rate => ReportFormat.Money(
                value.Value,
                string.IsNullOrWhiteSpace(currencyCode) ? "" : currencyCode),
            _ => value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public static TableColumnType ToColumnType(MetricUnit unit) =>
        unit switch
        {
            MetricUnit.Hours => TableColumnType.Hours,
            MetricUnit.Money => TableColumnType.Money,
            MetricUnit.Percent => TableColumnType.Percent,
            MetricUnit.Count => TableColumnType.Integer,
            MetricUnit.Rate => TableColumnType.Money,
            _ => TableColumnType.Text
        };
}

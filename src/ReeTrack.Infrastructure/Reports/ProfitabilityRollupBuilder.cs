using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;

namespace ReeTrack.Infrastructure.Reports;

/// <summary>Turns per-project cost rollups into revenue/margin rows and their per-currency totals.</summary>
internal static class ProfitabilityRollupBuilder
{
    public static (
        IReadOnlyList<ProjectProfitabilityDto> ProjectRows,
        IReadOnlyList<CurrencyFinancialKpisDto> ByCurrency)
        Build(IReadOnlyList<ProjectSummaryDto> projects, IReadOnlyDictionary<Guid, long> billableByProject)
    {
        var projectRows = projects
            .Select(p =>
            {
                var billableSeconds = billableByProject.GetValueOrDefault(p.ProjectId);
                var revenue = ReportRevenue.Calculate(
                    p.HourlyRate,
                    p.FixedFeeAmount,
                    p.TotalSeconds,
                    billableSeconds);
                var margin = ReportRevenue.Margin(revenue, p.CalculatedCost);
                var billing = SummaryReportAnalytics.BillingModel(p.HourlyRate, p.FixedFeeAmount);
                return new ProjectProfitabilityDto
                {
                    ProjectId = p.ProjectId,
                    Name = p.Name,
                    CurrencyCode = NormaliseCurrency(p.CurrencyCode),
                    ClientName = p.ClientName,
                    Status = p.Status,
                    BillingModel = billing switch
                    {
                        ProjectBillingModel.FixedFee => "FixedFee",
                        ProjectBillingModel.Hourly => "Hourly",
                        _ => "None"
                    },
                    HourlyRate = p.HourlyRate,
                    FixedFeeAmount = p.FixedFeeAmount,
                    TimeEstimateHours = p.TimeEstimateHours,
                    EstimateUsedPct = SummaryReportAnalytics.EstimateUsedPct(
                        p.TotalSeconds,
                        p.TimeEstimateHours),
                    TotalSeconds = p.TotalSeconds,
                    BillableSeconds = billableSeconds,
                    Revenue = revenue,
                    CalculatedCost = p.CalculatedCost,
                    NormalCost = p.NormalCost,
                    WeekendCost = p.WeekendCost,
                    HolidayCost = p.HolidayCost,
                    OvertimeCost = p.OvertimeCost,
                    Margin = margin,
                    MarginPct = ReportRevenue.MarginPct(revenue, p.CalculatedCost)
                };
            })
            .OrderByDescending(p => p.Margin)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byCurrency = projectRows
            .GroupBy(p => p.CurrencyCode)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var revenue = g.Sum(p => p.Revenue);
                var cost = g.Sum(p => p.CalculatedCost);
                var billableSeconds = g.Sum(p => p.BillableSeconds);
                return new CurrencyFinancialKpisDto
                {
                    CurrencyCode = g.Key,
                    Revenue = revenue,
                    Cost = cost,
                    Margin = ReportRevenue.Margin(revenue, cost),
                    MarginPct = ReportRevenue.MarginPct(revenue, cost),
                    BillableHours = SummaryReportAnalytics.Hours(billableSeconds),
                    TotalSeconds = g.Sum(p => p.TotalSeconds),
                    ProjectCount = g.Count()
                };
            })
            .ToList();

        return (projectRows, byCurrency);
    }

    private static string NormaliseCurrency(string? currencyCode) =>
        string.IsNullOrWhiteSpace(currencyCode)
            ? SummaryReportAnalytics.NoCurrencyCode
            : currencyCode.Trim().ToUpperInvariant();
}

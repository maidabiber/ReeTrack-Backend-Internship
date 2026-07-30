using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Reports;

/// <summary>
/// Derived figures for the summary report — the arithmetic, with no formatting.
///
/// These live in Application rather than beside the export writers because they are
/// report semantics, not presentation: the API surfaces several of them so the SPA
/// and the exports agree, and the planned detailed / workload / profitability reports
/// need the same definitions. Anything that returns a display string belongs in the
/// Infrastructure-side ReportFormat instead.
/// </summary>
public static class SummaryReportAnalytics
{
    /// <summary>Sentinel currency code for entries/projects with no currency set. The one
    /// named constant for this — do not repeat the "—" literal elsewhere.</summary>
    public const string NoCurrencyCode = "—";

    /// <summary>Decimal hours from duration seconds (sortable / summable).</summary>
    public static decimal Hours(long seconds) =>
        Round(seconds / 3600m, 4);

    /// <summary>Share of <paramref name="partSeconds"/> of <paramref name="totalSeconds"/> on a 0–100 scale.</summary>
    public static decimal PctOfTotal(long partSeconds, long totalSeconds) =>
        totalSeconds <= 0
            ? 0m
            : Round(partSeconds * 100m / totalSeconds, 2);

    /// <summary>Billable share of total on a 0–100 scale.</summary>
    public static decimal BillablePct(long billableSeconds, long totalSeconds) =>
        PctOfTotal(billableSeconds, totalSeconds);

    /// <summary>Actual logged hours as a percent of the project's time estimate (0–100+), or null when unset.</summary>
    public static decimal? EstimateUsedPct(long actualSeconds, decimal? estimateHours) =>
        estimateHours is null or <= 0m
            ? null
            : Round(Hours(actualSeconds) * 100m / estimateHours.Value, 1);

    /// <summary>Fixed-fee revenue minus labour cost (same currency), or null for non-fixed-fee projects.</summary>
    public static decimal? FixedFeeMargin(decimal? fixedFeeAmount, decimal calculatedCost) =>
        fixedFeeAmount is > 0m
            ? Round(fixedFeeAmount.Value - calculatedCost, 2)
            : null;

    /// <summary>Fixed fee wins when both are set — it is what the client is actually billed.</summary>
    public static ProjectBillingModel BillingModel(decimal? hourlyRate, decimal? fixedFeeAmount) =>
        fixedFeeAmount is > 0m ? ProjectBillingModel.FixedFee
        : hourlyRate is > 0m ? ProjectBillingModel.Hourly
        : ProjectBillingModel.None;

    /// <summary>Per-currency cost rollups plus that currency's highest-cost project.</summary>
    public static IReadOnlyList<CostByCurrencyInsight> CostByCurrency(SummaryReportDto model) =>
        CostBearingProjects(model)
            .GroupBy(NormaliseCurrency)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var totalCost = g.Sum(p => p.CalculatedCost);
                var totalSeconds = g.Sum(p => p.TotalSeconds);
                var hours = Hours(totalSeconds);
                var top = g.OrderByDescending(p => p.CalculatedCost)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .First();

                return new CostByCurrencyInsight(
                    g.Key,
                    g.Count(),
                    totalCost,
                    totalSeconds,
                    hours <= 0 ? 0m : Round(totalCost / hours, 2),
                    top.Name,
                    top.CalculatedCost);
            })
            .ToList();

    /// <summary>
    /// Per-currency spend split into mutually exclusive hour-type buckets
    /// (Normal + Weekend + Holiday + Overtime = TotalCost).
    /// </summary>
    public static IReadOnlyList<CostByHourTypeInsight> CostByHourType(SummaryReportDto model) =>
        CostBearingProjects(model)
            .GroupBy(NormaliseCurrency)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CostByHourTypeInsight(
                g.Key,
                Round(g.Sum(p => p.NormalCost), 2),
                Round(g.Sum(p => p.WeekendCost), 2),
                Round(g.Sum(p => p.HolidayCost), 2),
                Round(g.Sum(p => p.OvertimeCost), 2),
                Round(g.Sum(p => p.CalculatedCost), 2)))
            .ToList();

    /// <summary>
    /// Overtime / weekend / holiday hour rollups with the leading project per category.
    /// Currently unreferenced by the summary export — kept for the planned workload report.
    /// </summary>
    public static IReadOnlyList<ScheduleCategoryInsight> ScheduleInsights(SummaryReportDto model)
    {
        var totalSeconds = model.Kpis.TotalSeconds;
        return
        [
            BuildScheduleCategory("Overtime", model.Kpis.OvertimeHours, totalSeconds, model.Projects, p => p.OvertimeHours),
            BuildScheduleCategory("Weekend", model.Kpis.WeekendHours, totalSeconds, model.Projects, p => p.WeekendHours),
            BuildScheduleCategory("Holiday", model.Kpis.HolidayHours, totalSeconds, model.Projects, p => p.HolidayHours)
        ];
    }

    private static ScheduleCategoryInsight BuildScheduleCategory(
        string label,
        decimal hours,
        long totalSeconds,
        IReadOnlyList<ProjectSummaryDto> projects,
        Func<ProjectSummaryDto, decimal> selector)
    {
        var hoursSeconds = (long)Round(hours * 3600m, 0);
        var top = projects
            .Where(p => selector(p) > 0)
            .OrderByDescending(selector)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return new ScheduleCategoryInsight(
            label,
            hours,
            PctOfTotal(hoursSeconds, totalSeconds),
            top?.Name,
            top is null ? 0m : selector(top));
    }

    private static IReadOnlyList<ProjectSummaryDto> CostBearingProjects(SummaryReportDto model) =>
        model.Projects.Where(p => p.CalculatedCost > 0 || p.TotalSeconds > 0).ToList();

    private static string NormaliseCurrency(ProjectSummaryDto project) =>
        string.IsNullOrWhiteSpace(project.CurrencyCode)
            ? NoCurrencyCode
            : project.CurrencyCode.Trim().ToUpperInvariant();

    /// <summary>Half-away-from-zero — the single rounding rule for every derived report figure.</summary>
    private static decimal Round(decimal value, int digits) =>
        Math.Round(value, digits, MidpointRounding.AwayFromZero);
}

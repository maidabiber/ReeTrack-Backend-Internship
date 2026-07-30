using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;
using ReeTrack.Domain.Entities;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Reports;

/// <summary>Weekly revenue/cost/margin per currency for the Profitability report's sparkline.</summary>
internal static class ProfitabilityTrendBuilder
{
    public static IReadOnlyList<WeeklyFinancialTrendDto> Build(
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<ProjectProfitabilityDto> projects,
        DateOnly currentWeek,
        int weekCount = ReportAggregations.WeeklyTrendWeeks)
    {
        var oldestWeek = currentWeek.AddDays(-7 * (weekCount - 1));
        var projectById = projects.ToDictionary(p => p.ProjectId);
        var currencies = projects
            .Select(p => p.CurrencyCode)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        if (currencies.Count == 0)
            currencies = [SummaryReportAnalytics.NoCurrencyCode];

        // Cost attributed by share of project seconds in each week; fixed-fee revenue once
        // on the first week with activity; hourly revenue from that week's billable seconds.
        var costByWeekCurrency = new Dictionary<(DateOnly Week, string Currency), decimal>();
        var revenueByWeekCurrency = new Dictionary<(DateOnly Week, string Currency), decimal>();
        var fixedFeeAssigned = new HashSet<Guid>();

        var secondsByWeekProject = entries
            .Where(e => e.ProjectId is not null && e.Project is not null)
            .Select(e => (
                Week: TimesheetWeek.ToWeekStart(ReportMetadataResolver.ResolveEntryDate(e)),
                ProjectId: e.ProjectId!.Value,
                Seconds: (long)e.DurationSeconds,
                Billable: e.IsBillable ? (long)e.DurationSeconds : 0L))
            .Where(e => e.Week >= oldestWeek && e.Week <= currentWeek)
            .GroupBy(e => (e.Week, e.ProjectId))
            .ToDictionary(
                g => g.Key,
                g => (Total: g.Sum(x => x.Seconds), Billable: g.Sum(x => x.Billable)));

        var firstWeekByProject = secondsByWeekProject
            .GroupBy(kv => kv.Key.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => g.Min(kv => kv.Key.Week));

        foreach (var ((week, projectId), seconds) in secondsByWeekProject)
        {
            if (!projectById.TryGetValue(projectId, out var project))
                continue;

            var key = (week, project.CurrencyCode);
            var weekCost = project.TotalSeconds <= 0
                ? 0m
                : Math.Round(
                    project.CalculatedCost * seconds.Total / project.TotalSeconds,
                    2,
                    MidpointRounding.AwayFromZero);
            costByWeekCurrency[key] = costByWeekCurrency.GetValueOrDefault(key) + weekCost;

            // Re-derive the enum from the same inputs that produced project.BillingModel,
            // rather than string-comparing a value that was itself derived from this enum.
            var billingModel = SummaryReportAnalytics.BillingModel(project.HourlyRate, project.FixedFeeAmount);

            if (billingModel == ProjectBillingModel.FixedFee)
            {
                if (firstWeekByProject.TryGetValue(projectId, out var firstWeek)
                    && firstWeek == week
                    && fixedFeeAssigned.Add(projectId))
                {
                    revenueByWeekCurrency[key] =
                        revenueByWeekCurrency.GetValueOrDefault(key) + project.Revenue;
                }
            }
            else if (billingModel == ProjectBillingModel.Hourly && project.HourlyRate is > 0m && seconds.Billable > 0)
            {
                var weekRevenue = ReportRevenue.Calculate(
                    project.HourlyRate,
                    null,
                    seconds.Total,
                    seconds.Billable);
                revenueByWeekCurrency[key] =
                    revenueByWeekCurrency.GetValueOrDefault(key) + weekRevenue;
            }
        }

        var points = new List<WeeklyFinancialTrendDto>(weekCount * currencies.Count);
        for (var i = 0; i < weekCount; i++)
        {
            var week = oldestWeek.AddDays(7 * i);
            foreach (var currency in currencies)
            {
                var key = (week, currency);
                var revenue = Math.Round(
                    revenueByWeekCurrency.GetValueOrDefault(key),
                    2,
                    MidpointRounding.AwayFromZero);
                var cost = Math.Round(
                    costByWeekCurrency.GetValueOrDefault(key),
                    2,
                    MidpointRounding.AwayFromZero);
                points.Add(new WeeklyFinancialTrendDto
                {
                    WeekStartDate = week,
                    CurrencyCode = currency,
                    Revenue = revenue,
                    Cost = cost,
                    Margin = ReportRevenue.Margin(revenue, cost)
                });
            }
        }

        return points;
    }
}

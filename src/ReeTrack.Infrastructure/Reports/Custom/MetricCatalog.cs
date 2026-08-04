using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Application.Reports;

namespace ReeTrack.Infrastructure.Reports.Custom;

internal sealed record MetricInput(
    IReadOnlyList<EntryRow> Rows,
    CustomReportContext Context,
    long GrandTotalSeconds);

internal sealed record MetricDefinition(
    string Id,
    string Label,
    MetricUnit Unit,
    MetricScope Scope,
    Func<MetricInput, decimal?> Aggregate,
    bool NeedsCost = false,
    bool NeedsProjects = false,
    bool NeedsHourTargets = false);

internal static class MetricCatalog
{
    public static IReadOnlyDictionary<string, MetricDefinition> All { get; } =
        new Dictionary<string, MetricDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["totalHours"] = Entry("totalHours", "Total hours", MetricUnit.Hours,
                input => SummaryReportAnalytics.Hours(SumSeconds(input.Rows))),
            ["billableHours"] = Entry("billableHours", "Billable hours", MetricUnit.Hours,
                input => SummaryReportAnalytics.Hours(SumSeconds(input.Rows.Where(r => r.IsBillable)))),
            ["nonBillableHours"] = Entry("nonBillableHours", "Non-billable hours", MetricUnit.Hours,
                input => SummaryReportAnalytics.Hours(SumSeconds(input.Rows.Where(r => !r.IsBillable)))),
            ["billablePct"] = Entry("billablePct", "Billable %", MetricUnit.Percent,
                input => SummaryReportAnalytics.BillablePct(
                    SumSeconds(input.Rows.Where(r => r.IsBillable)),
                    SumSeconds(input.Rows))),
            ["entryCount"] = Entry("entryCount", "Entries", MetricUnit.Count,
                input => input.Rows.Count),
            ["activeMembers"] = Entry("activeMembers", "Active members", MetricUnit.Count,
                input => input.Rows.Select(r => r.UserId).Distinct().Count()),
            ["activeProjects"] = Entry("activeProjects", "Active projects", MetricUnit.Count,
                input => input.Rows.Where(r => r.ProjectId is not null).Select(r => r.ProjectId!.Value).Distinct().Count()),
            ["overtimeHours"] = Entry("overtimeHours", "Overtime hours", MetricUnit.Hours,
                input => ReportRounding.Hours(input.Rows.Sum(r => r.Cost?.OvertimeHours ?? 0m)),
                NeedsCost: true),
            ["weekendHours"] = Entry("weekendHours", "Weekend hours", MetricUnit.Hours,
                input => ReportRounding.Hours(input.Rows.Sum(r => r.Cost?.WeekendHours ?? 0m)),
                NeedsCost: true),
            ["holidayHours"] = Entry("holidayHours", "Holiday hours", MetricUnit.Hours,
                input => ReportRounding.Hours(input.Rows.Sum(r => r.Cost?.HolidayHours ?? 0m)),
                NeedsCost: true),
            ["unassignedHours"] = Entry("unassignedHours", "Unassigned hours", MetricUnit.Hours,
                input => SummaryReportAnalytics.Hours(
                    SumSeconds(input.Rows.Where(r => r.ProjectId is null)))),
            ["labourCost"] = Entry("labourCost", "Labour cost", MetricUnit.Money,
                input => ReportRounding.Cost(input.Rows.Sum(r => r.Cost?.CalculatedCost ?? 0m)),
                NeedsCost: true),
            ["normalCost"] = Entry("normalCost", "Normal cost", MetricUnit.Money,
                input => ReportRounding.Cost(input.Rows.Sum(r => r.Cost?.NormalCost ?? 0m)),
                NeedsCost: true),
            ["overtimeCost"] = Entry("overtimeCost", "Overtime cost", MetricUnit.Money,
                input => ReportRounding.Cost(input.Rows.Sum(r => r.Cost?.OvertimeCost ?? 0m)),
                NeedsCost: true),
            ["avgEntryLength"] = Entry("avgEntryLength", "Avg entry length", MetricUnit.Hours,
                input =>
                {
                    if (input.Rows.Count == 0)
                        return 0m;
                    // Divide in decimal — integer seconds / count truncates before conversion.
                    return ReportRounding.Hours(
                        SummaryReportAnalytics.Hours(SumSeconds(input.Rows)) / input.Rows.Count);
                }),
            ["avgHoursPerDay"] = Entry("avgHoursPerDay", "Avg hours / day", MetricUnit.Hours,
                input =>
                {
                    var days = input.Rows.Select(r => r.Date).Distinct().Count();
                    if (days == 0)
                        return 0m;
                    return ReportRounding.Hours(
                        SummaryReportAnalytics.Hours(SumSeconds(input.Rows)) / days);
                }),
            ["effectiveHourlyRate"] = Project("effectiveHourlyRate", "Effective hourly rate", MetricUnit.Rate,
                totals =>
                {
                    var billableHours = SummaryReportAnalytics.Hours(totals.BillableSeconds);
                    return billableHours <= 0m
                        ? null
                        : Math.Round(totals.Revenue / billableHours, 2, MidpointRounding.AwayFromZero);
                }),
            ["revenue"] = Project("revenue", "Revenue", MetricUnit.Money,
                totals => totals.Revenue),
            ["margin"] = Project("margin", "Margin", MetricUnit.Money,
                totals => ReportRevenue.Margin(totals.Revenue, totals.Cost)),
            ["marginPct"] = Project("marginPct", "Margin %", MetricUnit.Percent,
                totals => ReportRevenue.MarginPct(totals.Revenue, totals.Cost)),
            ["estimateUsedPct"] = Project("estimateUsedPct", "Estimate used %", MetricUnit.Percent,
                totals => SummaryReportAnalytics.EstimateUsedPct(
                    totals.EstimatedProjectSeconds,
                    totals.EstimateHours)),
            ["capacityUtilizationPct"] = new MetricDefinition(
                "capacityUtilizationPct",
                "Capacity utilisation %",
                MetricUnit.Percent,
                MetricScope.User,
                input =>
                {
                    var byUser = input.Rows.GroupBy(r => r.UserId).ToList();
                    if (byUser.Count == 0)
                        return null;

                    decimal totalTarget = 0m;
                    decimal totalHours = 0m;
                    foreach (var group in byUser)
                    {
                        if (!input.Context.WeeklyHourTargets.TryGetValue(group.Key, out var weeklyTarget)
                            || weeklyTarget <= 0m)
                            continue;

                        var weeks = group.Select(r => r.WeekStart).Distinct().Count();
                        if (weeks <= 0)
                            continue;

                        totalTarget += weeklyTarget * weeks;
                        totalHours += SummaryReportAnalytics.Hours(SumSeconds(group));
                    }

                    if (totalTarget <= 0m)
                        return null;

                    return Math.Round(totalHours * 100m / totalTarget, 2, MidpointRounding.AwayFromZero);
                },
                NeedsHourTargets: true),
        };

    public static MetricDefinition GetRequired(string id) =>
        All.TryGetValue(id, out var definition)
            ? definition
            : throw Application.Common.Exceptions.AppErrors.Validation(
                $"Unknown metric '{id}'.");

    private static MetricDefinition Entry(
        string id,
        string label,
        MetricUnit unit,
        Func<MetricInput, decimal?> aggregate,
        bool NeedsCost = false) =>
        new(id, label, unit, MetricScope.Entry, aggregate, NeedsCost);

    private static MetricDefinition Project(
        string id,
        string label,
        MetricUnit unit,
        Func<ProjectTotals, decimal?> compute) =>
        new(
            id,
            label,
            unit,
            MetricScope.Project,
            input => compute(AccumulateProjects(input)),
            NeedsCost: true,
            NeedsProjects: true);

    /// <summary>
    /// Additive parts of every project the current rows touch. Project-scope metrics derive
    /// their value from these sums rather than from per-project values added together, so
    /// ratio metrics (margin %, estimate used %) stay real percentages.
    /// </summary>
    private readonly record struct ProjectTotals(
        decimal Revenue,
        decimal Cost,
        long BillableSeconds,
        long EstimatedProjectSeconds,
        decimal? EstimateHours);

    private static ProjectTotals AccumulateProjects(MetricInput input)
    {
        var billableSecondsByProject = input.Rows
            .Where(r => r.ProjectId is not null && r.IsBillable)
            .GroupBy(r => r.ProjectId!.Value)
            .ToDictionary(g => g.Key, SumSeconds);

        var projectIds = input.Rows
            .Where(r => r.ProjectId is not null)
            .Select(r => r.ProjectId!.Value)
            .ToHashSet();

        if (projectIds.Count == 0)
            return new ProjectTotals(0m, 0m, 0L, 0L, null);

        decimal revenue = 0m;
        decimal cost = 0m;
        long billableSeconds = 0L;
        long estimatedProjectSeconds = 0L;
        decimal estimateHours = 0m;
        var sawEstimate = false;

        foreach (var summary in input.Context.ProjectSummaries.Where(p => projectIds.Contains(p.ProjectId)))
        {
            var projectBillableSeconds = billableSecondsByProject.GetValueOrDefault(summary.ProjectId);
            billableSeconds += projectBillableSeconds;
            revenue += ReportRevenue.Calculate(
                summary.HourlyRate,
                summary.FixedFeeAmount,
                summary.TotalSeconds,
                projectBillableSeconds);
            cost += summary.CalculatedCost;

            // Only projects that actually carry an estimate belong in the estimate ratio.
            if (summary.TimeEstimateHours is > 0m)
            {
                sawEstimate = true;
                estimateHours += summary.TimeEstimateHours.Value;
                estimatedProjectSeconds += summary.TotalSeconds;
            }
        }

        return new ProjectTotals(
            ReportRounding.Cost(revenue),
            ReportRounding.Cost(cost),
            billableSeconds,
            estimatedProjectSeconds,
            sawEstimate ? estimateHours : null);
    }

    private static long SumSeconds(IEnumerable<EntryRow> rows) =>
        rows.Sum(r => r.DurationSeconds);
}

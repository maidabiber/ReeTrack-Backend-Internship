using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

/// <summary>
/// Project-scope metrics aggregate the parts (revenue, cost, seconds) and derive the ratio
/// once. Adding per-project percentages together produced values like 150% margin.
/// </summary>
public class ProjectMetricTests
{
    private static readonly Guid AlphaId = Guid.NewGuid();
    private static readonly Guid BetaId = Guid.NewGuid();

    private static EntryRow Row(Guid projectId, long seconds, bool billable = true) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ada",
            projectId,
            projectId == AlphaId ? "Alpha" : "Beta",
            Guid.NewGuid(),
            "Acme",
            null,
            "(No task)",
            [],
            billable,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 6, 29),
            "EUR",
            seconds,
            null,
            null);

    private static ProjectSummaryDto Summary(
        Guid projectId,
        string name,
        long totalSeconds,
        decimal calculatedCost,
        decimal? hourlyRate = null,
        decimal? timeEstimateHours = null) =>
        new()
        {
            ProjectId = projectId,
            Name = name,
            CurrencyCode = "EUR",
            TotalSeconds = totalSeconds,
            CalculatedCost = calculatedCost,
            NormalCost = calculatedCost,
            WeekendCost = 0m,
            HolidayCost = 0m,
            OvertimeCost = 0m,
            OvertimeHours = 0m,
            WeekendHours = 0m,
            HolidayHours = 0m,
            HourlyRate = hourlyRate,
            TimeEstimateHours = timeEstimateHours
        };

    private static decimal? Evaluate(string metricId, IReadOnlyList<ProjectSummaryDto> summaries)
    {
        // Alpha: 10 billable hours, Beta: 10 billable hours.
        var rows = new[] { Row(AlphaId, 36000), Row(BetaId, 36000) };
        var context = CustomReportContext.ForTests(rows, summaries);
        var input = new MetricInput(rows, context, 72000);
        return MetricCatalog.GetRequired(metricId).Aggregate(input);
    }

    [Fact]
    public void Revenue_SumsAcrossProjects()
    {
        var value = Evaluate("revenue",
        [
            Summary(AlphaId, "Alpha", 36000, 400m, hourlyRate: 100m),
            Summary(BetaId, "Beta", 36000, 900m, hourlyRate: 100m),
        ]);

        // 10h × 100 for each project.
        Assert.Equal(2000m, value);
    }

    [Fact]
    public void MarginPct_IsRatioOfTotals_NotSumOfPercentages()
    {
        var value = Evaluate("marginPct",
        [
            // Alpha: 1000 revenue / 400 cost = 60% margin.
            Summary(AlphaId, "Alpha", 36000, 400m, hourlyRate: 100m),
            // Beta:  1000 revenue / 900 cost = 10% margin.
            Summary(BetaId, "Beta", 36000, 900m, hourlyRate: 100m),
        ]);

        // (2000 - 1300) / 2000 = 35%. Summing the two project percentages would give 70%.
        Assert.Equal(35m, value);
    }

    [Fact]
    public void EstimateUsedPct_IgnoresProjectsWithoutAnEstimate()
    {
        var value = Evaluate("estimateUsedPct",
        [
            // 10h logged against a 20h estimate.
            Summary(AlphaId, "Alpha", 36000, 400m, hourlyRate: 100m, timeEstimateHours: 20m),
            // No estimate — must not drag the ratio toward zero.
            Summary(BetaId, "Beta", 36000, 900m, hourlyRate: 100m),
        ]);

        Assert.Equal(50m, value);
    }

    [Fact]
    public void EstimateUsedPct_IsNullWhenNoProjectHasAnEstimate()
    {
        var value = Evaluate("estimateUsedPct",
        [
            Summary(AlphaId, "Alpha", 36000, 400m, hourlyRate: 100m),
            Summary(BetaId, "Beta", 36000, 900m, hourlyRate: 100m),
        ]);

        Assert.Null(value);
    }

    [Fact]
    public void EffectiveHourlyRate_DividesTotalRevenueByTotalBillableHours()
    {
        var value = Evaluate("effectiveHourlyRate",
        [
            Summary(AlphaId, "Alpha", 36000, 400m, hourlyRate: 100m),
            Summary(BetaId, "Beta", 36000, 900m, hourlyRate: 50m),
        ]);

        // (1000 + 500) revenue over 20 billable hours.
        Assert.Equal(75m, value);
    }

    [Fact]
    public void EffectiveHourlyRate_IsProjectScoped_SoItCannotBeGroupedByDay()
    {
        // Project-scope metrics read period-wide project summaries; allowing them against a
        // time dimension would repeat each project's full revenue in every bucket.
        Assert.Equal(
            Application.Common.Models.CustomReports.MetricScope.Project,
            MetricCatalog.GetRequired("effectiveHourlyRate").Scope);
    }
}

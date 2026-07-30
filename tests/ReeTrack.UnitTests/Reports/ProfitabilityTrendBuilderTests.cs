using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Reports;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class ProfitabilityTrendBuilderTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // 2026-01-12 and 2026-01-19 are both Mondays (see ProjectCostCalculatorTests for the
    // same anchor date), so these line up cleanly with TimesheetWeek's Monday-based weeks.
    private static readonly DateOnly Week1 = new(2026, 1, 12);
    private static readonly DateOnly Week2 = new(2026, 1, 19);

    [Fact]
    public void Build_FixedFeeProject_RecognizesRevenueOnlyInFirstWeekWithActivity()
    {
        var projectId = Guid.NewGuid();
        var project = FixedFeeProject(projectId, "EUR", revenue: 500m, calculatedCost: 100m, totalSeconds: 7200);
        var entries = new List<TimeEntry>
        {
            Entry(projectId, Week1, seconds: 3600, billable: true),
            Entry(projectId, Week2, seconds: 3600, billable: true)
        };

        var points = ProfitabilityTrendBuilder.Build(entries, [project], Week2, weekCount: 2);

        var week1Point = Assert.Single(points, p => p.WeekStartDate == Week1 && p.CurrencyCode == "EUR");
        Assert.Equal(500m, week1Point.Revenue);
        Assert.Equal(50m, week1Point.Cost); // 100 * 3600/7200

        var week2Point = Assert.Single(points, p => p.WeekStartDate == Week2 && p.CurrencyCode == "EUR");
        Assert.Equal(0m, week2Point.Revenue); // already recognized in week1, not repeated
        Assert.Equal(50m, week2Point.Cost);
    }

    [Fact]
    public void Build_HourlyProject_RecognizesRevenueProRataPerWeekFromBillableSeconds()
    {
        var projectId = Guid.NewGuid();
        var project = HourlyProject(projectId, "EUR", hourlyRate: 50m, calculatedCost: 90m, totalSeconds: 5400);
        var entries = new List<TimeEntry>
        {
            Entry(projectId, Week1, seconds: 1800, billable: true),
            Entry(projectId, Week2, seconds: 3600, billable: true)
        };

        var points = ProfitabilityTrendBuilder.Build(entries, [project], Week2, weekCount: 2);

        var week1Point = Assert.Single(points, p => p.WeekStartDate == Week1 && p.CurrencyCode == "EUR");
        Assert.Equal(25m, week1Point.Revenue); // 1800/3600 * 50
        Assert.Equal(30m, week1Point.Cost); // 90 * 1800/5400

        var week2Point = Assert.Single(points, p => p.WeekStartDate == Week2 && p.CurrencyCode == "EUR");
        Assert.Equal(50m, week2Point.Revenue); // 3600/3600 * 50
        Assert.Equal(60m, week2Point.Cost); // 90 * 3600/5400
    }

    [Fact]
    public void Build_HourlyProject_NonBillableSecondsContributeCostButNoRevenue()
    {
        var projectId = Guid.NewGuid();
        var project = HourlyProject(projectId, "EUR", hourlyRate: 50m, calculatedCost: 100m, totalSeconds: 3600);
        var entries = new List<TimeEntry> { Entry(projectId, Week1, seconds: 3600, billable: false) };

        var points = ProfitabilityTrendBuilder.Build(entries, [project], Week1, weekCount: 1);

        var point = Assert.Single(points, p => p.CurrencyCode == "EUR");
        Assert.Equal(0m, point.Revenue); // no billable seconds -> no revenue recognized
        Assert.Equal(100m, point.Cost); // cost still attributed by seconds share
    }

    [Fact]
    public void Build_ZeroFillsEveryWeekInWindow_EvenWithNoActivity()
    {
        var projectId = Guid.NewGuid();
        var project = HourlyProject(projectId, "EUR", hourlyRate: 50m, calculatedCost: 50m, totalSeconds: 3600);
        // Only week2 has an entry; week1 must still appear, zero-filled.
        var entries = new List<TimeEntry> { Entry(projectId, Week2, seconds: 3600, billable: true) };

        var points = ProfitabilityTrendBuilder.Build(entries, [project], Week2, weekCount: 2);

        Assert.Equal(2, points.Count);
        var week1Point = Assert.Single(points, p => p.WeekStartDate == Week1);
        Assert.Equal(0m, week1Point.Revenue);
        Assert.Equal(0m, week1Point.Cost);
        Assert.Equal(0m, week1Point.Margin);
    }

    [Fact]
    public void Build_MultipleCurrencies_KeepsSeparateSeries_NeverSummed()
    {
        var eurProjectId = Guid.NewGuid();
        var usdProjectId = Guid.NewGuid();
        var eurProject = FixedFeeProject(eurProjectId, "EUR", revenue: 500m, calculatedCost: 100m, totalSeconds: 3600);
        var usdProject = FixedFeeProject(usdProjectId, "USD", revenue: 800m, calculatedCost: 200m, totalSeconds: 3600);
        var entries = new List<TimeEntry>
        {
            Entry(eurProjectId, Week1, seconds: 3600, billable: true),
            Entry(usdProjectId, Week1, seconds: 3600, billable: true)
        };

        var points = ProfitabilityTrendBuilder.Build(entries, [eurProject, usdProject], Week1, weekCount: 1);

        Assert.Equal(2, points.Count);
        var eurPoint = Assert.Single(points, p => p.CurrencyCode == "EUR");
        Assert.Equal(500m, eurPoint.Revenue);
        var usdPoint = Assert.Single(points, p => p.CurrencyCode == "USD");
        Assert.Equal(800m, usdPoint.Revenue);
    }

    [Fact]
    public void Build_NoProjects_FallsBackToNoCurrencySentinel_AndZeroFillsAllWeeks()
    {
        var points = ProfitabilityTrendBuilder.Build([], [], Week2, weekCount: 2);

        Assert.Equal(2, points.Count);
        Assert.All(points, p => Assert.Equal("—", p.CurrencyCode));
        Assert.All(points, p => Assert.Equal(0m, p.Revenue));
        Assert.All(points, p => Assert.Equal(0m, p.Cost));
    }

    private static ProjectProfitabilityDto FixedFeeProject(
        Guid projectId, string currencyCode, decimal revenue, decimal calculatedCost, long totalSeconds) =>
        new()
        {
            ProjectId = projectId,
            Name = "Fixed fee project",
            CurrencyCode = currencyCode,
            ClientName = "Acme",
            Status = "Active",
            BillingModel = "FixedFee",
            FixedFeeAmount = revenue,
            HourlyRate = null,
            TotalSeconds = totalSeconds,
            BillableSeconds = totalSeconds,
            Revenue = revenue,
            CalculatedCost = calculatedCost,
            NormalCost = calculatedCost,
            WeekendCost = 0m,
            HolidayCost = 0m,
            OvertimeCost = 0m,
            Margin = revenue - calculatedCost,
            MarginPct = null
        };

    private static ProjectProfitabilityDto HourlyProject(
        Guid projectId, string currencyCode, decimal hourlyRate, decimal calculatedCost, long totalSeconds) =>
        new()
        {
            ProjectId = projectId,
            Name = "Hourly project",
            CurrencyCode = currencyCode,
            ClientName = "Acme",
            Status = "Active",
            BillingModel = "Hourly",
            FixedFeeAmount = null,
            HourlyRate = hourlyRate,
            TotalSeconds = totalSeconds,
            BillableSeconds = totalSeconds,
            Revenue = 0m,
            CalculatedCost = calculatedCost,
            NormalCost = calculatedCost,
            WeekendCost = 0m,
            HolidayCost = 0m,
            OvertimeCost = 0m,
            Margin = 0m,
            MarginPct = null
        };

    private static TimeEntry Entry(Guid projectId, DateOnly week, int seconds, bool billable) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            ProjectId = projectId,
            Project = new Project { Id = projectId, Name = "Stub" },
            IsBillable = billable,
            DurationSeconds = seconds,
            StartedAtUtc = week.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
            Status = TimeEntryStatus.Confirmed,
            CreatedAtUtc = week.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
            UpdatedAtUtc = week.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc)
        };
}

using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;
using ReeTrack.Domain.ValueObjects;
using Xunit;

namespace ReeTrack.UnitTests.ProjectCosts;

public class ProjectCostCalculatorTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Calculate_UsesMaxOfUserAndProjectRate()
    {
        var project = CreateProject(hourlyRate: 50m);
        var user = CreateUser();
        user.AssignInitialHourlyRate(new DateOnly(2026, 1, 1));
        user.ChangeHourlyRate(Money.Eur(80m), new DateOnly(2026, 2, 1));

        var entries = new List<TimeEntry>
        {
            CreateEntry(user.Id, durationSeconds: 3600, startedAt: new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc))
        };

        var result = CreateCalculator().Calculate(
            project, entries, entries, user.HourlyRates.ToList(), new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(80m, result.CalculatedCost);
        Assert.Equal(1m, result.TotalHours);
    }

    [Fact]
    public void Calculate_UsesProjectRate_WhenHigherThanUserRate()
    {
        var project = CreateProject(hourlyRate: 100m);
        var user = CreateUser();
        user.AssignInitialHourlyRate(new DateOnly(2026, 1, 1));

        var entries = new List<TimeEntry>
        {
            CreateEntry(user.Id, durationSeconds: 7200, startedAt: new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc))
        };

        var result = CreateCalculator().Calculate(
            project, entries, entries, user.HourlyRates.ToList(), new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(200m, result.CalculatedCost);
        Assert.Equal(2m, result.TotalHours);
    }

    [Fact]
    public void Calculate_DefaultsToZero_WhenNoRatesExist()
    {
        var project = CreateProject(hourlyRate: null);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, durationSeconds: 3600, startedAt: new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc))
        };

        var result = CreateCalculator().Calculate(
            project, entries, entries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(0m, result.CalculatedCost);
        Assert.Equal(1m, result.TotalHours);
    }

    [Fact]
    public void Calculate_IgnoresPendingAndDeletedEntries()
    {
        var project = CreateProject(hourlyRate: 50m);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc), TimeEntryStatus.Confirmed),
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 16, 9, 0, 0, DateTimeKind.Utc), TimeEntryStatus.Pending),
            CreateEntry(
                UserId,
                3600,
                new DateTime(2026, 1, 17, 9, 0, 0, DateTimeKind.Utc),
                TimeEntryStatus.Confirmed,
                deletedAtUtc: new DateTime(2026, 1, 18, 0, 0, 0, DateTimeKind.Utc))
        };

        var result = CreateCalculator().Calculate(
            project, entries, entries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(50m, result.CalculatedCost);
        Assert.Equal(1m, result.TotalHours);
        Assert.Equal(0m, result.WeekendHours);
    }

    [Fact]
    public void Calculate_PassesRateThroughBaseMultiplier()
    {
        var project = CreateProject(hourlyRate: 40m);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, durationSeconds: 1800, startedAt: new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc))
        };

        var calculator = new ProjectCostCalculator([new BaseRateMultiplier()]);
        var result = calculator.Calculate(
            project, entries, entries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(20m, result.CalculatedCost);
        Assert.Equal(0.5m, result.TotalHours);
    }

    [Fact]
    public void Calculate_SumsMultipleEntries()
    {
        var project = CreateProject(hourlyRate: 60m);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc)),
            CreateEntry(UserId, 1800, new DateTime(2026, 1, 16, 9, 0, 0, DateTimeKind.Utc))
        };

        var result = CreateCalculator().Calculate(
            project, entries, entries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(90m, result.CalculatedCost);
        Assert.Equal(1.5m, result.TotalHours);
    }

    [Fact]
    public void Calculate_AppliesWeekendRate()
    {
        var project = CreateProject(hourlyRate: 100m);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 17, 9, 0, 0, DateTimeKind.Utc))
        };

        var result = CreateCalculator().Calculate(
            project, entries, entries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(150m, result.CalculatedCost);
        Assert.Equal(1m, result.TotalHours);
        Assert.Equal(1m, result.WeekendHours);
        Assert.Equal(0m, result.HolidayHours);
        Assert.Equal(0m, result.OvertimeHours);
        Assert.Equal(0m, result.NormalCost);
        Assert.Equal(150m, result.WeekendCost);
        Assert.Equal(0m, result.HolidayCost);
        Assert.Equal(0m, result.OvertimeCost);
    }

    [Fact]
    public void Calculate_AppliesHolidayRate()
    {
        var project = CreateProject(hourlyRate: 100m);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc))
        };

        var result = CreateCalculator().Calculate(
            project,
            entries,
            entries,
            [],
            new HashSet<DateOnly> { new(2026, 1, 15) },
            RateMultiplierConfig.Defaults);

        Assert.Equal(200m, result.CalculatedCost);
        Assert.Equal(1m, result.TotalHours);
        Assert.Equal(0m, result.WeekendHours);
        Assert.Equal(1m, result.HolidayHours);
        Assert.Equal(0m, result.OvertimeHours);
        Assert.Equal(0m, result.NormalCost);
        Assert.Equal(0m, result.WeekendCost);
        Assert.Equal(200m, result.HolidayCost);
        Assert.Equal(0m, result.OvertimeCost);
    }

    [Fact]
    public void Calculate_AddsWeekendAndHolidayPremiums()
    {
        var project = CreateProject(hourlyRate: 100m);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 17, 9, 0, 0, DateTimeKind.Utc))
        };

        var result = CreateCalculator().Calculate(
            project,
            entries,
            entries,
            [],
            new HashSet<DateOnly> { new(2026, 1, 17) },
            RateMultiplierConfig.Defaults);

        Assert.Equal(250m, result.CalculatedCost);
        Assert.Equal(1m, result.TotalHours);
        Assert.Equal(1m, result.WeekendHours);
        Assert.Equal(1m, result.HolidayHours);
        Assert.Equal(0m, result.OvertimeHours);
        Assert.Equal(250m, result.WeekendCost);
        Assert.Equal(0m, result.HolidayCost);
    }

    [Fact]
    public void Calculate_AddsWeekendAndOvertimePremiums()
    {
        var project = CreateProject(hourlyRate: 100m);
        var precedingEntry = CreateEntry(
            UserId,
            40 * 3600,
            new DateTime(2026, 1, 16, 9, 0, 0, DateTimeKind.Utc));
        var weekendOvertimeEntry = CreateEntry(
            UserId,
            3600,
            new DateTime(2026, 1, 17, 9, 0, 0, DateTimeKind.Utc));
        var projectEntries = new List<TimeEntry> { weekendOvertimeEntry };
        var crossProjectEntries = new List<TimeEntry> { precedingEntry, weekendOvertimeEntry };

        var result = CreateCalculator().Calculate(
            project, projectEntries, crossProjectEntries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(200m, result.CalculatedCost);
        Assert.Equal(1m, result.TotalHours);
        Assert.Equal(1m, result.WeekendHours);
        Assert.Equal(1m, result.OvertimeHours);
        Assert.Equal(200m, result.WeekendCost);
        Assert.Equal(0m, result.OvertimeCost);
    }

    [Fact]
    public void Calculate_UsesWeightedRate_WhenEntryCrossesOvertimeThreshold()
    {
        var project = CreateProject(hourlyRate: 100m);
        var precedingEntry = CreateEntry(
            UserId,
            38 * 3600,
            new DateTime(2026, 1, 12, 9, 0, 0, DateTimeKind.Utc));
        var thresholdCrossingEntry = CreateEntry(
            UserId,
            4 * 3600,
            new DateTime(2026, 1, 13, 9, 0, 0, DateTimeKind.Utc));
        var projectEntries = new List<TimeEntry> { thresholdCrossingEntry };
        var crossProjectEntries = new List<TimeEntry> { thresholdCrossingEntry, precedingEntry };

        var result = CreateCalculator().Calculate(
            project, projectEntries, crossProjectEntries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(500m, result.CalculatedCost);
        Assert.Equal(4m, result.TotalHours);
        Assert.Equal(2m, result.OvertimeHours);
        Assert.Equal(250m, result.NormalCost);
        Assert.Equal(250m, result.OvertimeCost);
        Assert.Equal(0m, result.WeekendCost);
    }

    [Fact]
    public void Calculate_UsesConfiguredPremiumsAndThreshold()
    {
        var project = CreateProject(hourlyRate: 100m);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 17, 9, 0, 0, DateTimeKind.Utc))
        };
        var config = new RateMultiplierConfig(
            WeekendPremium: 0.25m,
            HolidayPremium: 1.0m,
            OvertimePremium: 0.5m,
            WeeklyOvertimeThresholdHours: 40m);

        var result = CreateCalculator().Calculate(project, entries, entries, [], new HashSet<DateOnly>(), config);

        // 100 * (1 + 0.25) = 125
        Assert.Equal(125m, result.CalculatedCost);
    }

    [Fact]
    public void Calculate_UsesCrossProjectEntriesToDetermineWeeklyOvertime()
    {
        var project = CreateProject(hourlyRate: 100m);
        var otherProjectEntry = CreateEntry(
            UserId,
            40 * 3600,
            new DateTime(2026, 1, 12, 9, 0, 0, DateTimeKind.Utc));
        var projectEntry = CreateEntry(
            UserId,
            3600,
            new DateTime(2026, 1, 13, 9, 0, 0, DateTimeKind.Utc));
        var projectEntries = new List<TimeEntry> { projectEntry };
        var crossProjectEntries = new List<TimeEntry> { projectEntry, otherProjectEntry };

        var result = CreateCalculator().Calculate(
            project, projectEntries, crossProjectEntries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(150m, result.CalculatedCost);
        Assert.Equal(1m, result.TotalHours);
        Assert.Equal(1m, result.OvertimeHours);
    }

    [Fact]
    public void Calculate_IsolatesOvertimeByUserAndMondayBasedWeek()
    {
        var project = CreateProject(hourlyRate: 100m);
        var otherUserId = Guid.NewGuid();
        var lastWeekEntry = CreateEntry(
            UserId,
            40 * 3600,
            new DateTime(2026, 1, 12, 9, 0, 0, DateTimeKind.Utc));
        var otherUserEntry = CreateEntry(
            otherUserId,
            40 * 3600,
            new DateTime(2026, 1, 19, 9, 0, 0, DateTimeKind.Utc));
        var projectEntry = CreateEntry(
            UserId,
            3600,
            new DateTime(2026, 1, 19, 9, 0, 0, DateTimeKind.Utc));
        var projectEntries = new List<TimeEntry> { projectEntry };
        var crossProjectEntries = new List<TimeEntry>
        {
            lastWeekEntry,
            otherUserEntry,
            projectEntry
        };

        var result = CreateCalculator().Calculate(
            project, projectEntries, crossProjectEntries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(100m, result.CalculatedCost);
        Assert.Equal(1m, result.TotalHours);
        Assert.Equal(0m, result.OvertimeHours);
    }

    [Fact]
    public void Calculate_GroupsCostsByTask_AndIgnoresUntaskedInTaskBreakdown()
    {
        var project = CreateProject(hourlyRate: 100m);
        var taskA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var taskB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 14, 9, 0, 0, DateTimeKind.Utc), projectTaskId: taskA),
            CreateEntry(UserId, 7200, new DateTime(2026, 1, 14, 11, 0, 0, DateTimeKind.Utc), projectTaskId: taskB),
            CreateEntry(UserId, 1800, new DateTime(2026, 1, 14, 14, 0, 0, DateTimeKind.Utc))
        };

        var result = CreateCalculator().Calculate(
            project, entries, entries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(350m, result.CalculatedCost);
        Assert.Equal(3.5m, result.TotalHours);
        Assert.Equal(2, result.TaskCosts.Count);

        var taskACost = Assert.Single(result.TaskCosts, task => task.ProjectTaskId == taskA);
        Assert.Equal(100m, taskACost.CalculatedCost);
        Assert.Equal(1m, taskACost.TotalHours);

        var taskBCost = Assert.Single(result.TaskCosts, task => task.ProjectTaskId == taskB);
        Assert.Equal(200m, taskBCost.CalculatedCost);
        Assert.Equal(2m, taskBCost.TotalHours);
    }

    [Fact]
    public void Calculate_RoundsOnceAtTheEnd_NotPerEntry()
    {
        // Regression test for a rounding bug: Calculate() must sum raw per-entry costs
        // and round only the total, matching the pre-refactor (master) behaviour.
        // Rounding each entry line first and summing the rounded values double-rounds
        // and drifts from the true total.
        //
        // A rate of 33.335 makes each 1-hour entry's cost land exactly on a rounding
        // boundary: 33.335 rounds (AwayFromZero) to 33.34, a +0.005 drift per entry.
        // Correct: sum 20 × 33.335 = 666.70 raw, then round once -> 666.70.
        // Buggy:   round each line to 33.34, sum 20 of them -> 666.80.
        var project = CreateProject(hourlyRate: 33.335m);

        // 20 entries, one per week (distinct Mondays), so none crosses the weekly
        // overtime threshold and none falls on a weekend or holiday.
        var entries = Enumerable.Range(0, 20)
            .Select(week => CreateEntry(
                UserId,
                durationSeconds: 3600,
                startedAt: new DateTime(2026, 1, 12, 9, 0, 0, DateTimeKind.Utc).AddDays(7 * week)))
            .ToList();

        var result = CreateCalculator().Calculate(
            project, entries, entries, [], new HashSet<DateOnly>(), RateMultiplierConfig.Defaults);

        Assert.Equal(666.70m, result.CalculatedCost);
    }

    [Fact]
    public void CalculateEntries_ReconcilesToProjectCalculate()
    {
        var project = CreateProject(hourlyRate: 100m);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 14, 9, 0, 0, DateTimeKind.Utc)),
            CreateEntry(UserId, 7200, new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc)),
            CreateEntry(UserId, 3600, new DateTime(2026, 1, 17, 9, 0, 0, DateTimeKind.Utc)) // Saturday
        };
        foreach (var entry in entries)
            entry.Project = project;

        var calc = CreateCalculator();
        var config = RateMultiplierConfig.Defaults;
        var holidays = new HashSet<DateOnly>();

        var projectResult = calc.Calculate(project, entries, entries, [], holidays, config);
        var lines = calc.CalculateEntries(entries, entries, [], holidays, config);

        Assert.Equal(3, lines.Count);
        Assert.Equal(projectResult.CalculatedCost, lines.Sum(line => line.CalculatedCost));
        Assert.Equal(projectResult.NormalCost, lines.Sum(line => line.NormalCost));
        Assert.Equal(projectResult.WeekendCost, lines.Sum(line => line.WeekendCost));
        Assert.Equal(projectResult.HolidayCost, lines.Sum(line => line.HolidayCost));
        Assert.Equal(projectResult.OvertimeCost, lines.Sum(line => line.OvertimeCost));
        Assert.True(Assert.Single(lines, line => line.IsWeekend).WeekendCost > 0);
    }

    private static ProjectCostCalculator CreateCalculator() =>
        new([
            new BaseRateMultiplier(),
            new WeekendRateMultiplier(),
            new HolidayRateMultiplier(),
            new OvertimeRateMultiplier()
        ]);

    private static Project CreateProject(decimal? hourlyRate) =>
        new()
        {
            Id = ProjectId,
            Name = "Test Project",
            HourlyRate = hourlyRate,
            CurrencyCode = "EUR"
        };

    private static User CreateUser() =>
        new()
        {
            Id = UserId,
            Email = "cost@reetrack.test",
            Status = UserStatus.Active,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    private static TimeEntry CreateEntry(
        Guid userId,
        int durationSeconds,
        DateTime startedAt,
        TimeEntryStatus status = TimeEntryStatus.Confirmed,
        DateTime? deletedAtUtc = null,
        Guid? projectTaskId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProjectId = ProjectId,
            ProjectTaskId = projectTaskId,
            DurationSeconds = durationSeconds,
            StartedAtUtc = startedAt,
            Status = status,
            DeletedAtUtc = deletedAtUtc,
            CreatedAtUtc = startedAt,
            UpdatedAtUtc = startedAt
        };
}

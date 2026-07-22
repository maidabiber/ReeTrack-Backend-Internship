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

        var calculator = CreateCalculator();
        var cost = calculator.Calculate(project, entries, user.HourlyRates.ToList());

        // MAX(80, 50) * 1h = 80
        Assert.Equal(80m, cost);
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

        var calculator = CreateCalculator();
        var cost = calculator.Calculate(project, entries, user.HourlyRates.ToList());

        // MAX(minimum wage, 100) * 2h = 200
        Assert.Equal(200m, cost);
    }

    [Fact]
    public void Calculate_DefaultsToZero_WhenNoRatesExist()
    {
        var project = CreateProject(hourlyRate: null);
        var entries = new List<TimeEntry>
        {
            CreateEntry(UserId, durationSeconds: 3600, startedAt: new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc))
        };

        var calculator = CreateCalculator();
        var cost = calculator.Calculate(project, entries, []);

        Assert.Equal(0m, cost);
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

        var calculator = CreateCalculator();
        var cost = calculator.Calculate(project, entries, []);

        Assert.Equal(50m, cost);
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
        var cost = calculator.Calculate(project, entries, []);

        // 0.5h * 40 = 20
        Assert.Equal(20m, cost);
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

        var calculator = CreateCalculator();
        var cost = calculator.Calculate(project, entries, []);

        // 1h*60 + 0.5h*60 = 90
        Assert.Equal(90m, cost);
    }

    private static ProjectCostCalculator CreateCalculator() =>
        new([new BaseRateMultiplier()]);

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
        DateTime? deletedAtUtc = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProjectId = ProjectId,
            DurationSeconds = durationSeconds,
            StartedAtUtc = startedAt,
            Status = status,
            DeletedAtUtc = deletedAtUtc,
            CreatedAtUtc = startedAt,
            UpdatedAtUtc = startedAt
        };
}

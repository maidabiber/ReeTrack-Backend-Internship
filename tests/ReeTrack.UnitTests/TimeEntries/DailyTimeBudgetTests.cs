using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.ValueObjects;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.TimeEntries;
using Xunit;

namespace ReeTrack.UnitTests.TimeEntries;

public class DailyTimeBudgetTests : IDisposable
{
    /// <summary>CEST / Europe/Berlin / Europe/Sarajevo in summer: UTC+2 → getTimezoneOffset() = -120.</summary>
    private const int CestOffsetMinutes = -120;

    private readonly AppDbContext _db;
    private readonly Guid _userId;
    private readonly DailyTimeBudget _budget;

    public DailyTimeBudgetTests()
    {
        _userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DailyTimeBudgetTests_{Guid.NewGuid()}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        SeedUser();
        _budget = new DailyTimeBudget(_db);
    }

    [Fact]
    public void GetLocalDayUtcRange_DateOnly_Cest_MapsLocalMidnightToUtcWindow()
    {
        var day = new DateOnly(2026, 8, 7);
        var (fromUtc, toUtc) = TimeEntryHelpers.GetLocalDayUtcRange(day, CestOffsetMinutes);

        Assert.Equal(new DateTime(2026, 8, 6, 22, 0, 0, DateTimeKind.Utc), fromUtc);
        Assert.Equal(new DateTime(2026, 8, 7, 22, 0, 0, DateTimeKind.Utc), toUtc);
    }

    [Fact]
    public void GetLocalDayUtcRange_Instant_Cest_LateEveningUtc_IsLocalNextDay()
    {
        // Local Aug 7 00:30 CEST
        var instant = new DateTime(2026, 8, 6, 22, 30, 0, DateTimeKind.Utc);
        var (fromUtc, toUtc) = TimeEntryHelpers.GetLocalDayUtcRange(instant, CestOffsetMinutes);

        Assert.Equal(new DateTime(2026, 8, 6, 22, 0, 0, DateTimeKind.Utc), fromUtc);
        Assert.Equal(new DateTime(2026, 8, 7, 22, 0, 0, DateTimeKind.Utc), toUtc);
    }

    [Fact]
    public void GetLocalDayUtcRange_Instant_Cest_EarlyMorningUtc_IsLocalSameDay()
    {
        // Local Aug 6 01:00 CEST
        var instant = new DateTime(2026, 8, 5, 23, 0, 0, DateTimeKind.Utc);
        var (fromUtc, toUtc) = TimeEntryHelpers.GetLocalDayUtcRange(instant, CestOffsetMinutes);

        Assert.Equal(new DateTime(2026, 8, 5, 22, 0, 0, DateTimeKind.Utc), fromUtc);
        Assert.Equal(new DateTime(2026, 8, 6, 22, 0, 0, DateTimeKind.Utc), toUtc);
    }

    [Fact]
    public void GetLocalDayUtcRange_OffsetZero_IsUtcCalendarDay()
    {
        var instant = new DateTime(2026, 8, 6, 22, 30, 0, DateTimeKind.Utc);
        var (fromUtc, toUtc) = TimeEntryHelpers.GetLocalDayUtcRange(instant, 0);

        Assert.Equal(new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc), fromUtc);
        Assert.Equal(new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc), toUtc);
    }

    [Fact]
    public async Task EnsureWithinBudget_Cest_LateEveningEntry_UsesLocalDayNotUtcDay()
    {
        var now = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

        // 23h already on UTC Aug 6 midday — would fill UTC Aug 6 under old logic
        SeedManual(
            new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc).AddHours(23),
            now);

        // Local Aug 7 00:30 = 22:30Z Aug 6 — local day Aug 7 still empty
        var localMorning = new DateTime(2026, 8, 6, 22, 30, 0, DateTimeKind.Utc);

        await _budget.EnsureWithinBudgetAsync(
            _userId,
            localMorning,
            newDurationSeconds: 2 * 3600,
            excludeEntryId: null,
            utcOffsetMinutes: CestOffsetMinutes);
    }

    [Fact]
    public async Task EnsureWithinBudget_OffsetZero_LateEveningEntry_CountsTowardUtcDay()
    {
        var now = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

        SeedManual(
            new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc).AddHours(23),
            now);

        var lateEveningUtc = new DateTime(2026, 8, 6, 22, 30, 0, DateTimeKind.Utc);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _budget.EnsureWithinBudgetAsync(
                _userId,
                lateEveningUtc,
                newDurationSeconds: 2 * 3600,
                excludeEntryId: null,
                utcOffsetMinutes: 0));

        Assert.Equal(ErrorCode.DurationLimitExceeded, ex.Code);
    }

    [Fact]
    public async Task EnsureWithinBudget_Cest_RejectsWhenLocalDayWouldExceed24h()
    {
        var now = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

        // Local Aug 7 01:00 CEST = 23:00Z Aug 6 — 23h already on local Aug 7
        SeedManual(
            new DateTime(2026, 8, 6, 23, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 6, 23, 0, 0, DateTimeKind.Utc).AddHours(23),
            now);

        // Another local Aug 7 entry (noon CEST = 10:00Z)
        var noonLocal = new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _budget.EnsureWithinBudgetAsync(
                _userId,
                noonLocal,
                newDurationSeconds: 2 * 3600,
                excludeEntryId: null,
                utcOffsetMinutes: CestOffsetMinutes));

        Assert.Equal(ErrorCode.DurationLimitExceeded, ex.Code);
    }

    private void SeedManual(DateTime startUtc, DateTime endUtc, DateTime now)
    {
        _db.TimeEntries.Add(TimeEntry.CreateManual(
            _userId,
            TimeRange.Create(startUtc, endUtc),
            "Existing",
            true,
            now));
        _db.SaveChanges();
    }

    private void SeedUser()
    {
        var now = DateTime.UtcNow;
        _db.Users.Add(new User
        {
            Id = _userId,
            Email = "daily-budget@test.local",
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();
}

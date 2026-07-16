using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.TimeEntries;
using ReeTrack.Infrastructure.Timesheets;
using Xunit;

namespace ReeTrack.UnitTests.Timesheets;

public class TimeEntryGuardServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _userId = Guid.NewGuid();

    public TimeEntryGuardServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TimeEntryGuardTests_{Guid.NewGuid()}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task EnsureEditable_NoTimesheetNoLock_Passes()
    {
        var guard = CreateGuard();

        await guard.EnsureEditableAsync(_userId, DateTime.UtcNow);
    }

    [Fact]
    public async Task EnsureEditable_LockedPeriod_Throws403BeforeTimesheetCheck()
    {
        var now = DateTime.UtcNow;
        await SeedTimesheet(TimesheetStatus.Submitted, now);
        var guard = CreateGuard(lockedBeforeUtc: now.AddDays(1));

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            guard.EnsureEditableAsync(_userId, now));

        Assert.Equal(403, ex.StatusCode);
    }

    [Theory]
    [InlineData(TimesheetStatus.Submitted)]
    [InlineData(TimesheetStatus.Approved)]
    public async Task EnsureEditable_WeekSubmittedOrApproved_Throws409(TimesheetStatus status)
    {
        var now = DateTime.UtcNow;
        await SeedTimesheet(status, now);
        var guard = CreateGuard();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            guard.EnsureEditableAsync(_userId, now));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task EnsureEditable_WeekRejected_Passes()
    {
        var now = DateTime.UtcNow;
        await SeedTimesheet(TimesheetStatus.Rejected, now);
        var guard = CreateGuard();

        await guard.EnsureEditableAsync(_userId, now);
    }

    [Fact]
    public async Task EnsureEditable_OtherUsersWeekSubmitted_Passes()
    {
        var now = DateTime.UtcNow;
        await SeedTimesheet(TimesheetStatus.Submitted, now);
        var guard = CreateGuard();

        await guard.EnsureEditableAsync(Guid.NewGuid(), now);
    }

    [Fact]
    public async Task EnsureEditable_AdjacentWeekSubmitted_Passes()
    {
        var now = DateTime.UtcNow;
        await SeedTimesheet(TimesheetStatus.Submitted, now.AddDays(-7));
        var guard = CreateGuard();

        await guard.EnsureEditableAsync(_userId, now);
    }

    private TimeEntryGuardService CreateGuard(DateTime? lockedBeforeUtc = null)
    {
        var lockedPeriod = new LockedPeriodService(Options.Create(new TimeEntryOptions
        {
            LockedBeforeUtc = lockedBeforeUtc
        }));
        return new TimeEntryGuardService(_db, lockedPeriod);
    }

    private async Task SeedTimesheet(TimesheetStatus status, DateTime instantInWeekUtc)
    {
        var now = DateTime.UtcNow;
        _db.Timesheets.Add(new Timesheet
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            WeekStartDate = TimesheetWeek.ToWeekStart(instantInWeekUtc),
            Status = status,
            SubmittedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await _db.SaveChangesAsync();
    }

    public void Dispose() => _db.Dispose();
}

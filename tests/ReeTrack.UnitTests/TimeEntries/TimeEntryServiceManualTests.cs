using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.TimeEntries;
using Xunit;

namespace ReeTrack.UnitTests.TimeEntries;

public class TimeEntryServiceManualTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _userId;
    private readonly TimeEntryService _service;

    public TimeEntryServiceManualTests()
    {
        _userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TimeEntryManualTests_{Guid.NewGuid()}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        SeedUser();

        _service = new TimeEntryService(_db, new FakeCurrentUser(_userId), new PermissiveLockedPeriodService());
    }

    [Fact]
    public async Task CreateManualEntry_ValidRange_PersistsWithComputedDuration()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-3);
        var endedAtUtc = DateTime.UtcNow.AddHours(-2);

        var result = await _service.CreateManualEntryAsync(
            "Design review",
            startedAtUtc,
            endedAtUtc);

        Assert.Equal("Manual", result.Entry.Mode);
        Assert.Equal(3600, result.Entry.DurationSeconds);
        Assert.Equal("Design review", result.Entry.Description);
        Assert.Null(result.OverlapWarning);

        var stored = await _db.TimeEntries.SingleAsync();
        Assert.Equal(TimeEntryMode.Manual, stored.Mode);
        Assert.Equal(3600, stored.DurationSeconds);
    }

    [Fact]
    public async Task CreateManualEntry_EndBeforeStart_Throws()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-1);
        var endedAtUtc = DateTime.UtcNow.AddHours(-2);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateManualEntryAsync(null, startedAtUtc, endedAtUtc));

        Assert.Equal("End time must be after start time.", ex.Message);
    }

    [Fact]
    public async Task CreateManualEntry_FutureRange_SavesSuccessfully()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(1);
        var endedAtUtc = DateTime.UtcNow.AddHours(2);

        var result = await _service.CreateManualEntryAsync(
            "Future planning",
            startedAtUtc,
            endedAtUtc);

        Assert.Equal("Manual", result.Entry.Mode);
        Assert.Equal(3600, result.Entry.DurationSeconds);
    }

    [Fact]
    public async Task CreateManualEntry_DurationOver24Hours_Throws()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-30);
        var endedAtUtc = DateTime.UtcNow.AddHours(-5);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateManualEntryAsync(null, startedAtUtc, endedAtUtc));

        Assert.Equal("Duration cannot exceed 24 hours.", ex.Message);
    }

    [Fact]
    public async Task CreateManualEntry_OverlapWithoutConfirm_ThrowsConflict()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-4);
        var endedAtUtc = DateTime.UtcNow.AddHours(-3);
        await _service.CreateManualEntryAsync("Existing entry", startedAtUtc, endedAtUtc);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateManualEntryAsync(
                "Overlapping entry",
                startedAtUtc.AddMinutes(30),
                endedAtUtc.AddMinutes(30)));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("overlap", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateManualEntry_OverlapWithRunningTimer_ThrowsConflict()
    {
        var now = DateTime.UtcNow;
        _db.TimeEntries.Add(new TimeEntry
        {
            UserId = _userId,
            Mode = TimeEntryMode.Timer,
            StartedAtUtc = now.AddMinutes(-5),
            EndedAtUtc = null,
            DurationSeconds = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateManualEntryAsync(
                "Overlapping entry",
                now.AddMinutes(-30),
                now));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("overlap", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateManualEntry_OverlapWithConfirm_SavesAndReturnsWarning()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-4);
        var endedAtUtc = DateTime.UtcNow.AddHours(-3);
        await _service.CreateManualEntryAsync("Existing entry", startedAtUtc, endedAtUtc);

        var result = await _service.CreateManualEntryAsync(
            "Overlapping entry",
            startedAtUtc.AddMinutes(30),
            endedAtUtc.AddMinutes(30),
            confirmOverlap: true);

        Assert.NotNull(result.OverlapWarning);
        Assert.Equal(2, await _db.TimeEntries.CountAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private void SeedUser()
    {
        var now = DateTime.UtcNow;
        _db.Users.Add(new User
        {
            Id = _userId,
            Email = "timer.test@reetrack.test",
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _db.SaveChanges();
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public IReadOnlyList<string> Roles { get; } = [];
        public bool IsAuthenticated => true;
    }

    private sealed class PermissiveLockedPeriodService : ILockedPeriodService
    {
        public Task EnsureEntryEditableAsync(DateTime startedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

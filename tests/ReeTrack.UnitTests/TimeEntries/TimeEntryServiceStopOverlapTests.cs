using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.ValueObjects;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.TimeEntries;
using ReeTrack.Infrastructure.Timesheets;
using Xunit;

namespace ReeTrack.UnitTests.TimeEntries;

public class TimeEntryServiceStopOverlapTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _userId;
    private readonly TimeEntryService _service;

    public TimeEntryServiceStopOverlapTests()
    {
        _userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TimeEntryStopOverlapTests_{Guid.NewGuid()}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        SeedUser();

        _service = TimeEntryServiceTestDependencies.CreateTimeEntryService(
            _db,
            new FakeCurrentUser(_userId),
            new TimeEntryGuardService(_db, new LockedPeriodService(Options.Create(new TimeEntryOptions()))));
    }

    [Fact]
    public async Task StopTimer_WithoutOverlap_ReturnsNoOverlapMeta()
    {
        await _service.CreateAsync(new TimeEntryInput { Description = "Solo timer" });

        var result = await _service.StopTimerAsync(new TimeEntryInput { Description = "Solo timer" });

        Assert.False(result.HasOverlap);
        Assert.Null(result.OverlapMessage);
        Assert.Null(result.SuggestedClipEndedAtUtc);
        Assert.Empty(result.OverlappingEntries);
        Assert.False(result.Entry.IsRunning);
        Assert.NotNull(result.Entry.EndedAtUtc);
    }

    [Fact]
    public async Task StopTimer_WithOverlap_PersistsAndReturnsSuggestedClip()
    {
        var now = DateTime.UtcNow;
        var overlapStart = now.AddMinutes(-20);
        var overlapEnd = now.AddMinutes(-10);

        // Existing manual entry that the running timer will overlap when stopped.
        _db.TimeEntries.Add(TimeEntry.CreateManual(
            _userId,
            TimeRange.Create(overlapStart, overlapEnd),
            "Existing meeting",
            true,
            now.AddHours(-1)));
        await _db.SaveChangesAsync();

        // Running timer that started before the meeting.
        var timer = TimeEntry.CreateTimer(_userId, "Focus work", true, now.AddMinutes(-30));
        _db.TimeEntries.Add(timer);
        await _db.SaveChangesAsync();

        var result = await _service.StopTimerAsync();

        Assert.True(result.HasOverlap);
        Assert.Equal("This entry overlaps with: Existing meeting.", result.OverlapMessage);
        Assert.Equal(overlapStart, result.SuggestedClipEndedAtUtc);
        Assert.Single(result.OverlappingEntries);
        Assert.Equal("Existing meeting", result.OverlappingEntries[0].Description);

        var stored = await _db.TimeEntries.SingleAsync(e => e.Id == result.Entry.Id);
        Assert.NotNull(stored.EndedAtUtc);
        Assert.True(stored.EndedAtUtc > overlapEnd);
        Assert.True(stored.DurationSeconds > 0);
    }

    [Fact]
    public async Task StopTimer_FullOverlap_SuggestedClipIsNull()
    {
        var now = DateTime.UtcNow;
        var coveringStart = now.AddHours(-2);
        var coveringEnd = now.AddMinutes(5);

        _db.TimeEntries.Add(TimeEntry.CreateManual(
            _userId,
            TimeRange.Create(coveringStart, coveringEnd),
            "All-day block",
            true,
            now.AddHours(-3)));
        await _db.SaveChangesAsync();

        // Timer started at/after the covering entry start → clip would yield non-positive duration.
        var timer = TimeEntry.CreateTimer(_userId, "Nested timer", true, coveringStart.AddMinutes(10));
        _db.TimeEntries.Add(timer);
        await _db.SaveChangesAsync();

        var result = await _service.StopTimerAsync();

        Assert.True(result.HasOverlap);
        Assert.Null(result.SuggestedClipEndedAtUtc);
        Assert.Single(result.OverlappingEntries);
        Assert.NotNull(result.Entry.EndedAtUtc);
    }

    private void SeedUser()
    {
        var now = DateTime.UtcNow;
        _db.Users.Add(new User
        {
            Id = _userId,
            Email = "stop-overlap@test.local",
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid UserId { get; } = userId;
        public IReadOnlyList<string> Roles { get; } = [];
        public bool IsAuthenticated => true;
    }
}

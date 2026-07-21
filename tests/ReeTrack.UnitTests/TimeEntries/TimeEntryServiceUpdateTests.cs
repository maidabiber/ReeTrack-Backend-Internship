using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.TimeEntries;
using ReeTrack.Infrastructure.Timesheets;
using Xunit;

namespace ReeTrack.UnitTests.TimeEntries;

public class TimeEntryServiceUpdateTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _userId;
    private readonly Guid _otherUserId;
    private readonly TimeEntryService _service;

    public TimeEntryServiceUpdateTests()
    {
        _userId = Guid.NewGuid();
        _otherUserId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TimeEntryUpdateTests_{Guid.NewGuid()}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        SeedUsers();

        _service = TimeEntryServiceTestDependencies.CreateTimeEntryService(
            _db,
            new FakeCurrentUser(_userId),
            new TimeEntryGuardService(_db, new LockedPeriodService(Options.Create(new TimeEntryOptions()))));
    }

    [Fact]
    public async Task UpdateTimeEntry_ChangesTimes_RecomputesDuration()
    {
        var entryId = await SeedManualEntry(
            DateTime.UtcNow.AddHours(-4),
            DateTime.UtcNow.AddHours(-3),
            "Original");

        var newStart = DateTime.UtcNow.AddHours(-5);
        var newEnd = DateTime.UtcNow.AddHours(-3);

        var result = await _service.UpdateTimeEntryAsync(entryId, new UpdateTimeEntryInput
        {
            Description = "Updated task",
            StartedAtUtc = newStart,
            EndedAtUtc = newEnd,
            IsBillable = false
        });

        Assert.Equal("Updated task", result.Entry.Description);
        Assert.False(result.Entry.IsBillable);
        Assert.Equal(7200, result.Entry.DurationSeconds);

        var stored = await _db.TimeEntries.SingleAsync(e => e.Id == entryId);
        Assert.Equal(7200, stored.DurationSeconds);
        Assert.Equal(newStart, stored.StartedAtUtc);
        Assert.Equal(newEnd, stored.EndedAtUtc);
    }

    [Fact]
    public async Task UpdateTimeEntry_NotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.UpdateTimeEntryAsync(Guid.NewGuid(), new UpdateTimeEntryInput
            {
                Description = "Missing",
                StartedAtUtc = DateTime.UtcNow.AddHours(-2),
                EndedAtUtc = DateTime.UtcNow.AddHours(-1),
                IsBillable = true
            }));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateTimeEntry_OtherUsersEntry_ThrowsNotFound()
    {
        var entryId = await SeedManualEntryForUser(
            _otherUserId,
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(-1));

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.UpdateTimeEntryAsync(entryId, new UpdateTimeEntryInput
            {
                Description = "Hack",
                StartedAtUtc = DateTime.UtcNow.AddHours(-2),
                EndedAtUtc = DateTime.UtcNow.AddHours(-1),
                IsBillable = true
            }));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateTimeEntry_RunningTimer_ThrowsConflict()
    {
        var now = DateTime.UtcNow;
        var entry = new TimeEntry
        {
            UserId = _userId,
            Mode = TimeEntryMode.Timer,
            StartedAtUtc = now.AddMinutes(-10),
            EndedAtUtc = null,
            DurationSeconds = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.UpdateTimeEntryAsync(entry.Id, new UpdateTimeEntryInput
            {
                Description = "Still running",
                StartedAtUtc = now.AddMinutes(-10),
                EndedAtUtc = now,
                IsBillable = true
            }));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateTimeEntry_Overlap_ThrowsConflict()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-4);
        var endedAtUtc = DateTime.UtcNow.AddHours(-3);
        await SeedManualEntry(startedAtUtc, endedAtUtc, "Existing");

        var entryId = await SeedManualEntry(
            DateTime.UtcNow.AddHours(-6),
            DateTime.UtcNow.AddHours(-5),
            "To move");

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.UpdateTimeEntryAsync(entryId, new UpdateTimeEntryInput
            {
                Description = "To move",
                StartedAtUtc = startedAtUtc.AddMinutes(30),
                EndedAtUtc = endedAtUtc.AddMinutes(30),
                IsBillable = true
            }));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateTimeEntry_DoesNotOverlapItself()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-4);
        var endedAtUtc = DateTime.UtcNow.AddHours(-3);
        var entryId = await SeedManualEntry(startedAtUtc, endedAtUtc, "Same entry");

        var result = await _service.UpdateTimeEntryAsync(entryId, new UpdateTimeEntryInput
        {
            Description = "Renamed only",
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = endedAtUtc,
            IsBillable = true
        });

        Assert.Equal("Renamed only", result.Entry.Description);
    }

    [Fact]
    public async Task UpdateTimeEntry_LockedPeriod_ThrowsForbidden()
    {
        var lockedService = new LockedPeriodService(Options.Create(new TimeEntryOptions
        {
            LockedBeforeUtc = DateTime.UtcNow.AddDays(-1)
        }));
        var service = TimeEntryServiceTestDependencies.CreateTimeEntryService(
            _db,
            new FakeCurrentUser(_userId),
            new TimeEntryGuardService(_db, lockedService));

        var entryId = await SeedManualEntry(
            DateTime.UtcNow.AddDays(-3),
            DateTime.UtcNow.AddDays(-3).AddHours(1),
            "Locked entry");

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateTimeEntryAsync(entryId, new UpdateTimeEntryInput
            {
                Description = "Try edit",
                StartedAtUtc = DateTime.UtcNow.AddHours(-2),
                EndedAtUtc = DateTime.UtcNow.AddHours(-1),
                IsBillable = true
            }));

        Assert.Equal(403, ex.StatusCode);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<Guid> SeedManualEntry(DateTime startedAtUtc, DateTime endedAtUtc, string description)
        => await SeedManualEntryForUser(_userId, startedAtUtc, endedAtUtc, description);

    private async Task<Guid> SeedManualEntryForUser(
        Guid userId,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        string description = "Entry")
    {
        var now = DateTime.UtcNow;
        var entry = new TimeEntry
        {
            UserId = userId,
            Description = description,
            Mode = TimeEntryMode.Manual,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = endedAtUtc,
            DurationSeconds = (int)(endedAtUtc - startedAtUtc).TotalSeconds,
            IsBillable = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry.Id;
    }

    private void SeedUsers()
    {
        var now = DateTime.UtcNow;
        _db.Users.AddRange(
            new User
            {
                Id = _userId,
                Email = "editor@reetrack.test",
                Status = UserStatus.Active,
                EmailVerified = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new User
            {
                Id = _otherUserId,
                Email = "other@reetrack.test",
                Status = UserStatus.Active,
                EmailVerified = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        _db.SaveChanges();
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid UserId { get; } = userId;
        public IReadOnlyList<string> Roles { get; } = [];
        public bool IsAuthenticated => true;
    }
}

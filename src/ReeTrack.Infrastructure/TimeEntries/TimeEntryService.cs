using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.TimeEntries;

public class TimeEntryService : ITimeEntryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TimeEntryService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TimeEntryDto?> GetActiveTimerAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var entry = await FindRunningTimerAsync(userId, cancellationToken);
        return entry is null ? null : MapTimeEntry(entry);
    }

    public async Task<TimeEntryDto> StartTimerAsync(
        string? description,
        bool isBillable = true,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var existing = await FindRunningTimerAsync(userId, cancellationToken);
        if (existing is not null)
            throw new AppException("A timer is already running.", 409);

        var now = DateTime.UtcNow;
        var entry = new TimeEntry
        {
            UserId = userId,
            Description = NormalizeDescription(description),
            IsBillable = isBillable,
            Mode = TimeEntryMode.Timer,
            StartedAtUtc = now,
            EndedAtUtc = null,
            DurationSeconds = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.TimeEntries.Add(entry);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppException("A timer is already running.", 409);
        }

        return MapTimeEntry(entry);
    }

    public async Task<TimeEntryDto> StopTimerAsync(
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var entry = await FindRunningTimerAsync(userId, cancellationToken, tracked: true)
            ?? throw new AppException("No timer is currently running.", 404);

        var now = DateTime.UtcNow;
        entry.EndedAtUtc = now;
        entry.DurationSeconds = (int)Math.Max(0, (now - entry.StartedAtUtc!.Value).TotalSeconds);

        if (description is not null)
            entry.Description = NormalizeDescription(description);

        entry.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);

        return MapTimeEntry(entry);
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.EndedAtUtc != null)
            .OrderByDescending(e => e.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(MapTimeEntry).ToList();
    }

    private Guid RequireUserId() =>
        _currentUser.UserId ?? throw new AppException("Authentication is required.", 401);

    private Task<TimeEntry?> FindRunningTimerAsync(
        Guid userId,
        CancellationToken cancellationToken,
        bool tracked = false)
    {
        var query = tracked
            ? _db.TimeEntries.AsQueryable()
            : _db.TimeEntries.AsNoTracking();

        return query.FirstOrDefaultAsync(
            e => e.UserId == userId &&
                 e.Mode == TimeEntryMode.Timer &&
                 e.EndedAtUtc == null,
            cancellationToken);
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var trimmed = description.Trim();
        return trimmed.Length > 1000 ? trimmed[..1000] : trimmed;
    }

    internal static TimeEntryDto MapTimeEntry(TimeEntry entry) =>
        new()
        {
            Id = entry.Id,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            Mode = entry.Mode.ToString(),
            StartedAtUtc = entry.StartedAtUtc,
            EndedAtUtc = entry.EndedAtUtc,
            DurationSeconds = entry.DurationSeconds,
            IsRunning = entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null
        };
}

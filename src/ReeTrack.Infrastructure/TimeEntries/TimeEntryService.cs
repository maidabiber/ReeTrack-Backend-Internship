using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.TimeEntries;

public class TimeEntryService : ITimeEntryService
{
    private const int MaxDurationSeconds = 24 * 60 * 60;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILockedPeriodService _lockedPeriod;

    public TimeEntryService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILockedPeriodService lockedPeriod)
    {
        _db = db;
        _currentUser = currentUser;
        _lockedPeriod = lockedPeriod;
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

    public async Task<CreateManualEntryResult> CreateManualEntryAsync(
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable = true,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        ValidateManualRange(startedAtUtc, endedAtUtc);
        await _lockedPeriod.EnsureEntryEditableAsync(startedAtUtc, cancellationToken);

        var durationSeconds = (int)(endedAtUtc - startedAtUtc).TotalSeconds;
        var overlapWarning = await BuildOverlapWarningAsync(
            userId,
            startedAtUtc,
            endedAtUtc,
            excludeEntryId: null,
            cancellationToken);

        if (overlapWarning is not null && !confirmOverlap)
            throw new AppException(overlapWarning, 409);

        var now = DateTime.UtcNow;
        var entry = new TimeEntry
        {
            UserId = userId,
            Description = NormalizeDescription(description),
            IsBillable = isBillable,
            Mode = TimeEntryMode.Manual,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = endedAtUtc,
            DurationSeconds = durationSeconds,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateManualEntryResult
        {
            Entry = MapTimeEntry(entry),
            OverlapWarning = confirmOverlap ? overlapWarning : null
        };
    }

    public async Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(
        Guid entryId,
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var entry = await _db.TimeEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, cancellationToken)
            ?? throw new AppException("Time entry not found.", 404);

        if (entry.EndedAtUtc is null)
            throw new AppException("Cannot edit a running timer entry.", 409);

        if (entry.StartedAtUtc is not null)
            await _lockedPeriod.EnsureEntryEditableAsync(entry.StartedAtUtc.Value, cancellationToken);

        ValidateManualRange(startedAtUtc, endedAtUtc);
        await _lockedPeriod.EnsureEntryEditableAsync(startedAtUtc, cancellationToken);

        var durationSeconds = (int)(endedAtUtc - startedAtUtc).TotalSeconds;
        var overlapWarning = await BuildOverlapWarningAsync(
            userId,
            startedAtUtc,
            endedAtUtc,
            excludeEntryId: entryId,
            cancellationToken);

        if (overlapWarning is not null && !confirmOverlap)
            throw new AppException(overlapWarning, 409);

        var now = DateTime.UtcNow;
        entry.Description = NormalizeDescription(description);
        entry.IsBillable = isBillable;
        entry.StartedAtUtc = startedAtUtc;
        entry.EndedAtUtc = endedAtUtc;
        entry.DurationSeconds = durationSeconds;
        entry.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateTimeEntryResult
        {
            Entry = MapTimeEntry(entry),
            OverlapWarning = confirmOverlap ? overlapWarning : null
        };
    }

    private static void ValidateManualRange(DateTime startedAtUtc, DateTime endedAtUtc)
    {
        if (endedAtUtc <= startedAtUtc)
            throw new AppException("End time must be after start time.");

        var durationSeconds = (endedAtUtc - startedAtUtc).TotalSeconds;
        if (durationSeconds > MaxDurationSeconds)
            throw new AppException("Duration cannot exceed 24 hours.");
    }

    private async Task<string?> BuildOverlapWarningAsync(
        Guid userId,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        Guid? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var overlapping = await _db.TimeEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.StartedAtUtc != null)
            .Where(e => excludeEntryId == null || e.Id != excludeEntryId)
            .Where(e =>
                e.StartedAtUtc < endedAtUtc &&
                (e.EndedAtUtc ?? now) > startedAtUtc)
            .OrderBy(e => e.StartedAtUtc)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (overlapping.Count == 0)
            return null;

        var labels = overlapping
            .Select(e => e.Description?.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Take(2)
            .ToList();

        if (labels.Count > 0)
            return $"This entry overlaps with: {string.Join(", ", labels)}. Save anyway?";

        return "This entry overlaps with an existing time entry. Save anyway?";
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListByDateRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var now = DateTime.UtcNow;

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.StartedAtUtc != null)
            .Where(e => e.StartedAtUtc < toUtc && (e.EndedAtUtc ?? now) > fromUtc)
            .OrderBy(e => e.StartedAtUtc)
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

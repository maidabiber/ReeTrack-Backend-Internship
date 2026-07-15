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
        return entry is null ? null : MapEntity(entry);
    }

    public async Task<TimeEntryDto> StartTimerAsync(
        StartTimerInput input,
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
            Description = NormalizeDescription(input.Description),
            IsBillable = input.IsBillable,
            Mode = TimeEntryMode.Timer,
            Status = TimeEntryStatus.Confirmed,
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

        return MapEntity(entry);
    }

    public async Task<TimeEntryDto> StopTimerAsync(
        StopTimerInput? input = null,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var entry = await FindRunningTimerAsync(userId, cancellationToken, tracked: true)
            ?? throw new AppException("No timer is currently running.", 404);

        var now = DateTime.UtcNow;
        entry.EndedAtUtc = now;
        entry.DurationSeconds = (int)Math.Max(0, (now - entry.StartedAtUtc!.Value).TotalSeconds);

        if (input?.Description is not null)
            entry.Description = NormalizeDescription(input.Description);

        entry.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);

        return MapEntity(entry);
    }

    public async Task<CreateManualEntryResult> CreateManualEntryAsync(
        CreateManualEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        ValidateManualRange(input.StartedAtUtc, input.EndedAtUtc);
        await _lockedPeriod.EnsureEntryEditableAsync(input.StartedAtUtc, cancellationToken);

        var durationSeconds = (int)(input.EndedAtUtc - input.StartedAtUtc).TotalSeconds;
        var overlapWarning = await BuildOverlapWarningAsync(
            userId,
            input.StartedAtUtc,
            input.EndedAtUtc,
            excludeEntryId: null,
            cancellationToken);

        if (overlapWarning is not null && !input.ConfirmOverlap)
            throw new AppException(overlapWarning, 409);

        var now = DateTime.UtcNow;
        var entry = new TimeEntry
        {
            UserId = userId,
            Description = NormalizeDescription(input.Description),
            IsBillable = input.IsBillable,
            Mode = TimeEntryMode.Manual,
            StartedAtUtc = input.StartedAtUtc,
            EndedAtUtc = input.EndedAtUtc,
            DurationSeconds = durationSeconds,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateManualEntryResult
        {
            Entry = MapEntity(entry),
            OverlapWarning = input.ConfirmOverlap ? overlapWarning : null
        };
    }

    public async Task<CreateManualEntryResult> CreateDurationOnlyEntryAsync(
        CreateDurationOnlyEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        ValidateDurationOnly(input.DurationSeconds);
        var normalizedEntryDateUtc = NormalizeEntryDateUtc(input.EntryDateUtc);

        var now = DateTime.UtcNow;
        await _lockedPeriod.EnsureEntryEditableAsync(normalizedEntryDateUtc, cancellationToken);

        var entry = new TimeEntry
        {
            UserId = userId,
            Description = NormalizeDescription(input.Description),
            IsBillable = input.IsBillable,
            Mode = TimeEntryMode.DurationOnly,
            StartedAtUtc = normalizedEntryDateUtc,
            EndedAtUtc = null,
            DurationSeconds = input.DurationSeconds,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateManualEntryResult
        {
            Entry = MapEntity(entry),
            OverlapWarning = null
        };
    }

    public async Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(
        Guid entryId,
        UpdateTimeEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var entry = await _db.TimeEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, cancellationToken)
            ?? throw new AppException("Time entry not found.", 404);

        if (entry.Mode == TimeEntryMode.DurationOnly)
            throw new AppException("Duration-only entries must be updated without start/end times.", 409);

        if (entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null)
            throw new AppException("Cannot edit a running timer entry.", 409);

        if (entry.Status == TimeEntryStatus.Pending)
            throw new AppException("Pending entries must be reviewed on the Approvals page.", 409);

        var overlapWarning = await ApplyTimedEntryUpdateAsync(
            entry,
            input,
            checkPreviousPeriodLock: true,
            cancellationToken);

        return new UpdateTimeEntryResult
        {
            Entry = MapEntity(entry),
            OverlapWarning = overlapWarning
        };
    }

    public async Task<UpdateTimeEntryResult> UpdateDurationOnlyEntryAsync(
        Guid entryId,
        UpdateDurationOnlyEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var entry = await _db.TimeEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, cancellationToken)
            ?? throw new AppException("Time entry not found.", 404);

        if (entry.Mode != TimeEntryMode.DurationOnly)
            throw new AppException("Only duration-only entries can be updated without start/end times.", 409);

        if (entry.Status == TimeEntryStatus.Pending)
            throw new AppException("Pending entries must be reviewed on the Approvals page.", 409);

        ValidateDurationOnly(input.DurationSeconds);
        var normalizedEntryDateUtc = NormalizeEntryDateUtc(input.EntryDateUtc);
        await _lockedPeriod.EnsureEntryEditableAsync(normalizedEntryDateUtc, cancellationToken);

        var now = DateTime.UtcNow;
        entry.Description = NormalizeDescription(input.Description);
        entry.IsBillable = input.IsBillable;
        entry.DurationSeconds = input.DurationSeconds;
        entry.StartedAtUtc = normalizedEntryDateUtc;
        entry.EndedAtUtc = null;
        entry.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateTimeEntryResult
        {
            Entry = MapEntity(entry),
            OverlapWarning = null
        };
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.SubmittedByUser)
            .Where(e =>
                (e.UserId == userId || e.SubmittedByUserId == userId) &&
                (
                    (e.Mode == TimeEntryMode.DurationOnly && e.DurationSeconds > 0) ||
                    (e.Mode != TimeEntryMode.DurationOnly && e.EndedAtUtc != null)
                ))
            .OrderByDescending(e => e.StartedAtUtc ?? e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var shareGroups = await LoadShareGroupsAsync(entries, cancellationToken);

        return entries
            .Select(e => MapEntity(
                e,
                e.SubmittedByUser?.DisplayName ?? e.SubmittedByUser?.Email,
                e.User.DisplayName ?? e.User.Email,
                shareGroups))
            .ToList();
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

        return entries.Select(e => MapEntity(e)).ToList();
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

    private async Task<IReadOnlyDictionary<Guid, List<TimeEntry>>> LoadShareGroupsAsync(
        IReadOnlyList<TimeEntry> entries,
        CancellationToken cancellationToken)
    {
        var shareGroupIds = entries
            .Where(e => e.ShareGroupId is not null)
            .Select(e => e.ShareGroupId!.Value)
            .Distinct()
            .ToList();

        if (shareGroupIds.Count == 0)
            return new Dictionary<Guid, List<TimeEntry>>();

        var siblings = await _db.TimeEntries
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.SubmittedByUser)
            .Where(e => e.ShareGroupId != null && shareGroupIds.Contains(e.ShareGroupId.Value))
            .ToListAsync(cancellationToken);

        return siblings
            .GroupBy(e => e.ShareGroupId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private async Task<string?> ApplyTimedEntryUpdateAsync(
        TimeEntry entry,
        UpdateTimeEntryInput input,
        bool checkPreviousPeriodLock,
        CancellationToken cancellationToken)
    {
        if (checkPreviousPeriodLock && entry.StartedAtUtc is not null)
            await _lockedPeriod.EnsureEntryEditableAsync(entry.StartedAtUtc.Value, cancellationToken);

        ValidateManualRange(input.StartedAtUtc, input.EndedAtUtc);
        await _lockedPeriod.EnsureEntryEditableAsync(input.StartedAtUtc, cancellationToken);

        var durationSeconds = (int)(input.EndedAtUtc - input.StartedAtUtc).TotalSeconds;
        var overlapWarning = await BuildOverlapWarningAsync(
            entry.UserId,
            input.StartedAtUtc,
            input.EndedAtUtc,
            excludeEntryId: entry.Id,
            cancellationToken);

        if (overlapWarning is not null && !input.ConfirmOverlap)
            throw new AppException(overlapWarning, 409);

        entry.Description = NormalizeDescription(input.Description);
        entry.IsBillable = input.IsBillable;
        entry.StartedAtUtc = input.StartedAtUtc;
        entry.EndedAtUtc = input.EndedAtUtc;
        entry.DurationSeconds = durationSeconds;
        entry.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return input.ConfirmOverlap ? overlapWarning : null;
    }

    private static void ValidateManualRange(DateTime startedAtUtc, DateTime endedAtUtc) =>
        TimeEntryHelpers.ValidateManualRange(startedAtUtc, endedAtUtc);

    private static void ValidateDurationOnly(int durationSeconds) =>
        TimeEntryHelpers.ValidateDurationOnly(durationSeconds);

    private static DateTime NormalizeEntryDateUtc(DateTime entryDateUtc) =>
        TimeEntryHelpers.NormalizeEntryDateUtc(entryDateUtc);

    private static string? NormalizeDescription(string? description) =>
        TimeEntryHelpers.NormalizeDescription(description);

    private static TimeEntryDto MapEntity(
        TimeEntry entry,
        string? submittedByDisplayName = null,
        string? assigneeDisplayName = null,
        IReadOnlyDictionary<Guid, List<TimeEntry>>? shareGroups = null) =>
        TimeEntryMapping.MapEntity(entry, submittedByDisplayName, assigneeDisplayName, shareGroups);
}

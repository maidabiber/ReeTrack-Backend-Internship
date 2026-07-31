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
    private readonly ITimeEntryGuardService _entryGuard;
    private readonly ITimeEntryAssociationService _associations;

    public TimeEntryService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITimeEntryGuardService entryGuard,
        ITimeEntryAssociationService associations)
    {
        _db = db;
        _currentUser = currentUser;
        _entryGuard = entryGuard;
        _associations = associations;
    }

    public async Task<TimeEntryDto?> GetActiveTimerAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await FindRunningTimerAsync(userId, cancellationToken);
        return entry is null ? null : MapEntity(entry);
    }

    public async Task<TimeEntryDto> StartTimerAsync(
        StartTimerInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var existing = await FindRunningTimerAsync(userId, cancellationToken);
        if (existing is not null)
            throw new AppException("A timer is already running.", 409, ErrorCode.AlreadyRunning);

        var now = DateTime.UtcNow;
        await _entryGuard.EnsureEditableAsync(userId, now, cancellationToken);

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

        await _associations.ApplyForCreateAsync(entry, input, cancellationToken);

        _db.TimeEntries.Add(entry);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppException("A timer is already running.", 409, ErrorCode.AlreadyRunning);
        }

        return MapEntity(entry);
    }

    public async Task<TimeEntryDto> StopTimerAsync(
        StopTimerInput? input = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await FindRunningTimerAsync(userId, cancellationToken, tracked: true)
            ?? throw new AppException("No timer is currently running.", 404, ErrorCode.NotFound);

        var now = DateTime.UtcNow;
        entry.EndedAtUtc = now;
        entry.DurationSeconds = (int)Math.Max(0, (now - entry.StartedAtUtc!.Value).TotalSeconds);

        if (input?.Description is not null)
            entry.Description = NormalizeDescription(input.Description);

        if (input is not null)
        {
            await _associations.ApplyForUpdateAsync(entry, input, cancellationToken);

            if (input.IsBillable is bool isBillable)
                entry.IsBillable = isBillable;
        }

        entry.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);

        return MapEntity(entry);
    }

    public async Task<CreateManualEntryResult> CreateManualEntryAsync(
        CreateManualEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        ValidateManualRange(input.StartedAtUtc, input.EndedAtUtc);
        await _entryGuard.EnsureEditableAsync(userId, input.StartedAtUtc, cancellationToken);

        var durationSeconds = (int)(input.EndedAtUtc - input.StartedAtUtc).TotalSeconds;
        await EnsureNoOverlapAsync(
            userId,
            input.StartedAtUtc,
            input.EndedAtUtc,
            excludeEntryId: null,
            cancellationToken);

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

        await _associations.ApplyForCreateAsync(entry, input, cancellationToken);

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateManualEntryResult
        {
            Entry = MapEntity(entry)
        };
    }

    public async Task<CreateManualEntryResult> CreateDurationOnlyEntryAsync(
        CreateDurationOnlyEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        ValidateDurationOnly(input.DurationSeconds);
        var normalizedEntryDateUtc = NormalizeEntryDateUtc(input.EntryDateUtc);

        var now = DateTime.UtcNow;
        await _entryGuard.EnsureEditableAsync(userId, normalizedEntryDateUtc, cancellationToken);

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

        await _associations.ApplyForCreateAsync(entry, input, cancellationToken);

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateManualEntryResult
        {
            Entry = MapEntity(entry)
        };
    }

    public async Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(
        Guid entryId,
        UpdateTimeEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await _db.TimeEntries
            .Include(e => e.Project)
            .Include(e => e.ProjectTask)
            .Include(e => e.TimeEntryTags)
                .ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, cancellationToken)
            ?? throw AppErrors.NotFound("Time entry");

        if (entry.Mode == TimeEntryMode.DurationOnly)
            throw AppErrors.Conflict("Duration-only entries must be updated without start/end times.");

        if (entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null)
            throw AppErrors.Conflict("Cannot edit a running timer entry.");

        if (entry.Status == TimeEntryStatus.Pending)
            throw AppErrors.Conflict("Pending entries must be reviewed on the Approvals page.");

        await ApplyTimedEntryUpdateAsync(
            entry,
            input,
            checkPreviousPeriodLock: true,
            cancellationToken);

        return new UpdateTimeEntryResult
        {
            Entry = MapEntity(entry)
        };
    }

    public async Task<UpdateTimeEntryResult> UpdateDurationOnlyEntryAsync(
        Guid entryId,
        UpdateDurationOnlyEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await _db.TimeEntries
            .Include(e => e.Project)
            .Include(e => e.ProjectTask)
            .Include(e => e.TimeEntryTags)
                .ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, cancellationToken)
            ?? throw AppErrors.NotFound("Time entry");

        if (entry.Mode != TimeEntryMode.DurationOnly)
            throw AppErrors.Conflict("Only duration-only entries can be updated without start/end times.");

        if (entry.Status == TimeEntryStatus.Pending)
            throw AppErrors.Conflict("Pending entries must be reviewed on the Approvals page.");

        ValidateDurationOnly(input.DurationSeconds);
        var normalizedEntryDateUtc = NormalizeEntryDateUtc(input.EntryDateUtc);
        if (entry.StartedAtUtc is not null)
            await _entryGuard.EnsureEditableAsync(entry.UserId, entry.StartedAtUtc.Value, cancellationToken);
        await _entryGuard.EnsureEditableAsync(entry.UserId, normalizedEntryDateUtc, cancellationToken);

        var now = DateTime.UtcNow;
        entry.Description = NormalizeDescription(input.Description);
        entry.IsBillable = input.IsBillable;
        entry.DurationSeconds = input.DurationSeconds;
        entry.StartedAtUtc = normalizedEntryDateUtc;
        entry.EndedAtUtc = null;
        entry.UpdatedAtUtc = now;

        await _associations.ApplyForUpdateAsync(entry, input, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateTimeEntryResult
        {
            Entry = MapEntity(entry)
        };
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.SubmittedByUser)
            .Include(e => e.Project)
            .Include(e => e.ProjectTask)
            .Include(e => e.TimeEntryTags)
                .ThenInclude(t => t.Tag)
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
        var userId = _currentUser.UserId;
        var now = DateTime.UtcNow;

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Include(e => e.Project)
            .Include(e => e.ProjectTask)
            .Include(e => e.TimeEntryTags)
                .ThenInclude(t => t.Tag)
            .Where(e => e.UserId == userId && e.StartedAtUtc != null)
            .Where(e => e.StartedAtUtc < toUtc && (e.EndedAtUtc ?? now) > fromUtc)
            .OrderBy(e => e.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(e => MapEntity(e)).ToList();
    }

    private Task<TimeEntry?> FindRunningTimerAsync(
        Guid userId,
        CancellationToken cancellationToken,
        bool tracked = false)
    {
        var query = tracked
            ? _db.TimeEntries.AsQueryable()
            : _db.TimeEntries.AsNoTracking();

        return query
            .Include(e => e.Project)
            .Include(e => e.ProjectTask)
            .Include(e => e.TimeEntryTags)
                .ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(
                e => e.UserId == userId &&
                     e.Mode == TimeEntryMode.Timer &&
                     e.EndedAtUtc == null,
                cancellationToken);
    }

    private async Task EnsureNoOverlapAsync(
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
            return;

        var labels = overlapping
            .Select(e => e.Description?.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Take(2)
            .ToList();

        if (labels.Count > 0)
            throw new AppException($"This entry overlaps with: {string.Join(", ", labels)}.", 409, ErrorCode.EntryOverlap);

        throw new AppException("This entry overlaps with an existing time entry.", 409, ErrorCode.EntryOverlap);
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

    private async Task ApplyTimedEntryUpdateAsync(
        TimeEntry entry,
        UpdateTimeEntryInput input,
        bool checkPreviousPeriodLock,
        CancellationToken cancellationToken)
    {
        if (checkPreviousPeriodLock && entry.StartedAtUtc is not null)
            await _entryGuard.EnsureEditableAsync(entry.UserId, entry.StartedAtUtc.Value, cancellationToken);

        ValidateManualRange(input.StartedAtUtc, input.EndedAtUtc);
        await _entryGuard.EnsureEditableAsync(entry.UserId, input.StartedAtUtc, cancellationToken);

        var durationSeconds = (int)(input.EndedAtUtc - input.StartedAtUtc).TotalSeconds;
        await EnsureNoOverlapAsync(
            entry.UserId,
            input.StartedAtUtc,
            input.EndedAtUtc,
            excludeEntryId: entry.Id,
            cancellationToken);

        entry.Description = NormalizeDescription(input.Description);
        entry.IsBillable = input.IsBillable;
        entry.StartedAtUtc = input.StartedAtUtc;
        entry.EndedAtUtc = input.EndedAtUtc;
        entry.DurationSeconds = durationSeconds;
        entry.UpdatedAtUtc = DateTime.UtcNow;

        await _associations.ApplyForUpdateAsync(entry, input, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
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

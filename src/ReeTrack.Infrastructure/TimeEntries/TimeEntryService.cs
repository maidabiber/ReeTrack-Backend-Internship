using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Notifications;
using ReeTrack.Application.Notifications.Events;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.ValueObjects;

namespace ReeTrack.Infrastructure.TimeEntries;

public class TimeEntryService : ITimeEntryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITimeEntryGuardService _entryGuard;
    private readonly ITimeEntryAssociationService _associations;
    private readonly ITimeEntryOverlapChecker _overlap;
    private readonly IDailyTimeBudget _dailyBudget;
    private readonly IDomainEventPublisher _eventPublisher;

    public TimeEntryService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITimeEntryGuardService entryGuard,
        ITimeEntryAssociationService associations,
        ITimeEntryOverlapChecker overlap,
        IDailyTimeBudget dailyBudget,
        IDomainEventPublisher eventPublisher)
    {
        _db = db;
        _currentUser = currentUser;
        _entryGuard = entryGuard;
        _associations = associations;
        _overlap = overlap;
        _dailyBudget = dailyBudget;
        _eventPublisher = eventPublisher;
    }

    public async Task<TimeEntryDto?> GetActiveTimerAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await FindRunningTimerAsync(userId, cancellationToken);
        return entry is null ? null : MapEntity(entry);
    }

    public async Task<StopTimerResultDto> StopTimerAsync(
        TimeEntryInput? input = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await FindRunningTimerAsync(userId, cancellationToken, tracked: true)
            ?? throw new AppException("No timer is currently running.", 404, ErrorCode.NotFound);

        entry.Stop(DateTime.UtcNow);

        if (input is not null)
        {
            entry.UpdateDetails(input.Description, input.IsBillable);
            await _associations.ApplyForUpdateAsync(entry, input, cancellationToken);
        }

        entry.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var overlapping = await _overlap.FindOverlapsAsync(
            userId,
            entry.StartedAtUtc!.Value,
            entry.EndedAtUtc!.Value,
            entry.Id,
            cancellationToken);

        DateTime? suggestedClipEndedAtUtc = null;
        string? overlapMessage = null;
        if (overlapping.Count > 0)
        {
            overlapMessage = TimeEntryOverlapChecker.BuildOverlapMessage(overlapping);
            var earliestOverlapStart = overlapping[0].StartedAtUtc;
            if (earliestOverlapStart > entry.StartedAtUtc.Value)
                suggestedClipEndedAtUtc = earliestOverlapStart;
        }

        return new StopTimerResultDto
        {
            Entry = MapEntity(entry),
            HasOverlap = overlapping.Count > 0,
            OverlapMessage = overlapMessage,
            SuggestedClipEndedAtUtc = suggestedClipEndedAtUtc,
            OverlappingEntries = overlapping
        };
    }

    public async Task DeleteAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await _db.TimeEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, cancellationToken)
            ?? throw AppErrors.NotFound("Time entry");

        if (entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null)
            throw AppErrors.Conflict("Cannot delete a running timer entry.");

        if (entry.StartedAtUtc is not null)
            await _entryGuard.EnsureEditableAsync(userId, entry.StartedAtUtc.Value, cancellationToken);

        entry.DeletedAtUtc = DateTime.UtcNow;
        entry.DeletedByUserId = userId;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TimeEntryDto> CreateAsync(
        TimeEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var now = DateTime.UtcNow;
        var entry = CreateEntity(userId, input, now);

        if (entry.Mode == TimeEntryMode.Timer)
        {
            var existing = await FindRunningTimerAsync(userId, cancellationToken);
            if (existing is not null)
                throw new AppException("A timer is already running.", 409, ErrorCode.AlreadyRunning);
        }

        await _entryGuard.EnsureEditableAsync(userId, entry.StartedAtUtc!.Value, cancellationToken);

        var effectiveEnd = entry.EndedAtUtc ?? entry.StartedAtUtc!.Value.AddSeconds(entry.DurationSeconds);
        await _overlap.EnsureNoOverlapAsync(userId, entry.StartedAtUtc!.Value, effectiveEnd, null, cancellationToken);

        var entryDate = entry.GetEntryDate();
        await _dailyBudget.EnsureWithinBudgetAsync(userId, entryDate, entry.DurationSeconds, null, cancellationToken);

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

    public async Task<TimeEntryDto> UpdateAsync(
        Guid entryId,
        TimeEntryInput input,
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

        var checkPeriodLock = entry.Status != TimeEntryStatus.Pending;

        if (entry.Mode == TimeEntryMode.DurationOnly)
        {
            if (input.DurationSeconds is not null && input.EntryDateUtc is not null)
            {
                if (checkPeriodLock && entry.StartedAtUtc is not null)
                    await _entryGuard.EnsureEditableAsync(entry.UserId, entry.StartedAtUtc.Value, cancellationToken);

                entry.UpdateDuration(input.DurationSeconds.Value, input.EntryDateUtc.Value);

                await _entryGuard.EnsureEditableAsync(entry.UserId, entry.StartedAtUtc!.Value, cancellationToken);

                entry.UpdateDetails(input.Description, input.IsBillable);

                await _dailyBudget.EnsureWithinBudgetAsync(
                    userId, entry.StartedAtUtc.Value.Date, entry.DurationSeconds, entry.Id, cancellationToken);

                await _associations.ApplyForUpdateAsync(entry, input, cancellationToken);
                entry.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return MapEntity(entry);
            }

            throw AppErrors.Conflict("Duration-only entries require entryDateUtc and durationSeconds.");
        }

        if (entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null)
            throw AppErrors.Conflict("Cannot edit a running timer entry.");

        if (checkPeriodLock && entry.StartedAtUtc is not null)
            await _entryGuard.EnsureEditableAsync(entry.UserId, entry.StartedAtUtc.Value, cancellationToken);

        if (input.StartedAtUtc is null || input.EndedAtUtc is null)
            throw AppErrors.Validation("Manual and timer entries require startedAtUtc and endedAtUtc.");

        var range = TimeRange.Create(input.StartedAtUtc.Value, input.EndedAtUtc.Value);

        entry.UpdateTiming(range);
        entry.UpdateDetails(input.Description, input.IsBillable);

        await _entryGuard.EnsureEditableAsync(entry.UserId, range.StartedAtUtc, cancellationToken);

        await _overlap.EnsureNoOverlapAsync(
            userId, range.StartedAtUtc, range.EndedAtUtc, entry.Id, cancellationToken);

        var entryDate = entry.GetEntryDate();
        await _dailyBudget.EnsureWithinBudgetAsync(userId, entryDate, entry.DurationSeconds, entry.Id, cancellationToken);

        await _associations.ApplyForUpdateAsync(entry, input, cancellationToken);
        entry.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapEntity(entry);
    }

    public async Task<TimeEntryDto> ShareEntryAsync(
        Guid entryId,
        IReadOnlyList<Guid> assigneeUserIds,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var source = await _db.TimeEntries
            .Include(e => e.TimeEntryTags)
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken)
            ?? throw AppErrors.NotFound("Time entry");

        if (source.Mode == TimeEntryMode.Timer && source.EndedAtUtc is null)
            throw AppErrors.Conflict("Cannot share a running timer entry.");

        if (source.StartedAtUtc is null)
            throw AppErrors.Validation("This entry cannot be shared.");

        var resolvedAssignees = await ResolveAssigneesAsync(assigneeUserIds, cancellationToken);

        foreach (var assignee in resolvedAssignees)
            await _entryGuard.EnsureEditableAsync(assignee.Id, source.StartedAtUtc.Value, cancellationToken);

        var shareGroupId = source.ShareGroupId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;

        if (source.ShareGroupId is null)
        {
            source.ShareGroupId = shareGroupId;
            source.UpdatedAtUtc = now;
        }

        var submitter = await _db.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == userId, cancellationToken);

        var pendingEntries = new List<TimeEntry>();
        foreach (var assignee in resolvedAssignees.OrderBy(a => a.DisplayName ?? a.Email))
        {
            var clone = source.ShareWith(assignee.Id, userId, shareGroupId, now);
            _associations.CopyAssociations(source, clone);
            _db.TimeEntries.Add(clone);
            pendingEntries.Add(clone);
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var entry in pendingEntries)
        {
            entry.User = resolvedAssignees.First(a => a.Id == entry.UserId);
            entry.SubmittedByUser = submitter;
        }

        foreach (var entry in pendingEntries)
        {
            var assignee = resolvedAssignees.First(a => a.Id == entry.UserId);
            await _eventPublisher.PublishAsync(new TimeEntrySharedNotification
            {
                EntryId = entry.Id,
                AssigneeUserId = assignee.Id,
                AssigneeName = assignee.DisplayName?.Trim() ?? assignee.Email,
                SubmitterName = submitter.DisplayName?.Trim() ?? submitter.Email,
                Description = entry.Description
            }, cancellationToken);
        }

        return MapEntity(source, assigneeDisplayName: submitter.DisplayName ?? submitter.Email);
    }

    public async Task<IReadOnlyList<TimeEntryDto>> CreateAndShareAsync(
        TimeEntryInput input,
        IReadOnlyList<Guid> assigneeUserIds,
        CancellationToken cancellationToken = default)
    {
        var created = await CreateAsync(input, cancellationToken);
        var shared = await ShareEntryAsync(created.Id, assigneeUserIds, cancellationToken);
        return [created, shared];
    }

    public async Task<TimeEntryDto> ApprovePendingEntryAsync(
        Guid entryId,
        TimeEntryInput? input = null,
        CancellationToken cancellationToken = default)
    {
        if (input is not null)
            await UpdateAsync(entryId, input, cancellationToken);

        var userId = _currentUser.UserId;
        var entry = await _db.TimeEntries
            .Include(e => e.SubmittedByUser)
            .Include(e => e.User)
            .FirstOrDefaultAsync(
                e => e.Id == entryId && e.UserId == userId && e.Status == TimeEntryStatus.Pending,
                cancellationToken)
            ?? throw AppErrors.NotFound("Pending time entry");

        if (entry.StartedAtUtc is not null)
            await _entryGuard.EnsureEditableAsync(entry.UserId, entry.StartedAtUtc.Value, cancellationToken);

        if (entry.Mode != TimeEntryMode.DurationOnly &&
            entry.StartedAtUtc is not null &&
            entry.EndedAtUtc is not null)
        {
            await _overlap.EnsureNoOverlapAsync(
                userId,
                entry.StartedAtUtc.Value,
                entry.EndedAtUtc.Value,
                entry.Id,
                cancellationToken);
        }

        await _dailyBudget.EnsureWithinBudgetAsync(
            userId,
            entry.GetEntryDate(),
            entry.DurationSeconds,
            entry.Id,
            cancellationToken);

        entry.Status = TimeEntryStatus.Confirmed;
        entry.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return TimeEntryMapping.MapEntity(
            entry,
            entry.SubmittedByUser?.DisplayName ?? entry.SubmittedByUser?.Email,
            entry.User.DisplayName ?? entry.User.Email);
    }

    public async Task RejectPendingEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await _db.TimeEntries
            .FirstOrDefaultAsync(
                e => e.Id == entryId && e.UserId == userId && e.Status == TimeEntryStatus.Pending,
                cancellationToken)
            ?? throw AppErrors.NotFound("Pending time entry");

        var now = DateTime.UtcNow;
        entry.DeletedAtUtc = now;
        entry.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entries = await BaseQuery()
            .Where(e =>
                e.UserId == userId &&
                (
                    (e.Mode == TimeEntryMode.DurationOnly && e.DurationSeconds > 0) ||
                    (e.Mode != TimeEntryMode.DurationOnly && e.EndedAtUtc != null)
                ))
            .OrderByDescending(e => e.StartedAtUtc ?? e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var shareGroups = await TimeEntryHelpers.LoadShareGroupsAsync(_db, entries, cancellationToken);

        return entries
            .Select(e => MapEntity(
                e,
                assigneeDisplayName: e.User.DisplayName ?? e.User.Email,
                shareGroups: shareGroups))
            .ToList();
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListByDateRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var now = DateTime.UtcNow;

        var entries = await BaseQuery()
            .Where(e => e.UserId == userId && e.StartedAtUtc != null)
            .Where(e => e.StartedAtUtc < toUtc && (e.EndedAtUtc ?? now) > fromUtc)
            .OrderBy(e => e.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(e => MapEntity(e)).ToList();
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListPendingEntriesAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var entries = await BaseQuery()
            .Include(e => e.SubmittedByUser)
            .Where(e => e.UserId == userId && e.Status == TimeEntryStatus.Pending)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var shareGroups = await TimeEntryHelpers.LoadShareGroupsAsync(_db, entries, cancellationToken);

        return entries
            .Select(e => TimeEntryMapping.MapEntity(
                e,
                e.SubmittedByUser?.DisplayName ?? e.SubmittedByUser?.Email,
                e.User.DisplayName ?? e.User.Email,
                shareGroups))
            .ToList();
    }

    private IQueryable<TimeEntry> BaseQuery() =>
        _db.TimeEntries
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.Project)
            .Include(e => e.ProjectTask)
            .Include(e => e.TimeEntryTags)
                .ThenInclude(t => t.Tag);

    private static TimeEntry CreateEntity(Guid userId, TimeEntryInput input, DateTime now)
    {
        if (input.StartedAtUtc.HasValue && input.EndedAtUtc.HasValue)
        {
            var range = TimeRange.Create(input.StartedAtUtc.Value, input.EndedAtUtc.Value);
            return TimeEntry.CreateManual(
                userId, range, input.Description, input.IsBillable ?? true, now);
        }

        if (input.EntryDateUtc.HasValue && input.DurationSeconds.HasValue)
        {
            return TimeEntry.CreateDurationOnly(
                userId, input.DurationSeconds.Value, input.EntryDateUtc.Value,
                input.Description, input.IsBillable ?? true, now);
        }

        return TimeEntry.CreateTimer(userId, input.Description, input.IsBillable ?? true, now);
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

    private async Task<List<User>> ResolveAssigneesAsync(
        IReadOnlyList<Guid> assigneeUserIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = assigneeUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0)
            throw new AppException("At least one teammate is required.", 400, ErrorCode.TeammatesRequired);

        var submitterId = _currentUser.UserId;
        if (distinctIds.Contains(submitterId))
            throw AppErrors.Validation("You cannot share a time entry with yourself.");

        var assignees = await _db.Users
            .AsNoTracking()
            .Where(u => distinctIds.Contains(u.Id) && u.Status == UserStatus.Active)
            .ToListAsync(cancellationToken);

        if (assignees.Count != distinctIds.Count)
            throw new AppException("One or more teammates were not found.", 404, ErrorCode.NotFound);

        return assignees;
    }

    private static TimeEntryDto MapEntity(
        TimeEntry entry,
        string? submittedByDisplayName = null,
        string? assigneeDisplayName = null,
        IReadOnlyDictionary<Guid, List<TimeEntry>>? shareGroups = null) =>
        TimeEntryMapping.MapEntity(entry, submittedByDisplayName, assigneeDisplayName, shareGroups);
}

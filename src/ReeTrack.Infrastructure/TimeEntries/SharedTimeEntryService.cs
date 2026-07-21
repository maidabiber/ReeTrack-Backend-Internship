
using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.TimeEntries;

public class SharedTimeEntryService : ISharedTimeEntryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITimeEntryService _timeEntries;
    private readonly ISharedTimeEntryEmailNotifier _emailNotifier;
    private readonly ITimeEntryGuardService _entryGuard;
    private readonly ITimeEntryAssociationService _associations;

    public SharedTimeEntryService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITimeEntryService timeEntries,
        ISharedTimeEntryEmailNotifier emailNotifier,
        ITimeEntryGuardService entryGuard,
        ITimeEntryAssociationService associations)
    {
        _db = db;
        _currentUser = currentUser;
        _timeEntries = timeEntries;
        _emailNotifier = emailNotifier;
        _entryGuard = entryGuard;
        _associations = associations;
    }

    public async Task<CreateSharedManualEntryResult> StopSharedTimerAsync(
        StopSharedTimerInput input,
        CancellationToken cancellationToken = default)
    {
        var running = await _timeEntries.GetActiveTimerAsync(cancellationToken)
            ?? throw new AppException("No timer is currently running.", 404);

        if (running.StartedAtUtc is null)
            throw new AppException("No timer is currently running.", 404);

        var assignees = await ResolveAssigneesAsync(input.AssigneeUserIds, cancellationToken);
        var startedAtUtc = running.StartedAtUtc.Value;
        var endedAtUtc = DateTime.UtcNow;

        var assigneeOverlap = await FindAssigneeOverlapMessageAsync(
            assignees,
            startedAtUtc,
            endedAtUtc,
            cancellationToken);
        if (assigneeOverlap is not null)
            throw new AppException(assigneeOverlap, 409);

        var stopped = await _timeEntries.StopTimerAsync(
            new StopTimerInput
            {
                Description = input.Description,
                ProjectId = input.ProjectId,
                ProjectTaskId = input.ProjectTaskId,
                TagIds = input.TagIds,
                IsBillable = input.IsBillable
            },
            cancellationToken);

        return await AttachShareToOwnedEntryAsync(
            stopped.Id,
            assignees,
            cancellationToken);
    }

    public async Task<CreateSharedManualEntryResult> CreateSharedManualEntryAsync(
        CreateSharedManualEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var assignees = await ResolveAssigneesAsync(input.AssigneeUserIds, cancellationToken);

        var assigneeOverlap = await FindAssigneeOverlapMessageAsync(
            assignees,
            input.StartedAtUtc,
            input.EndedAtUtc,
            cancellationToken);
        if (assigneeOverlap is not null)
            throw new AppException(assigneeOverlap, 409);

        var mine = await _timeEntries.CreateManualEntryAsync(
            new CreateManualEntryInput
            {
                Description = input.Description,
                StartedAtUtc = input.StartedAtUtc,
                EndedAtUtc = input.EndedAtUtc,
                IsBillable = input.IsBillable,
                ProjectId = input.ProjectId,
                ProjectTaskId = input.ProjectTaskId,
                TagIds = input.TagIds
            },
            cancellationToken);

        return await AttachShareToOwnedEntryAsync(
            mine.Entry.Id,
            assignees,
            cancellationToken);
    }

    public async Task<CreateSharedManualEntryResult> CreateSharedDurationOnlyEntryAsync(
        CreateSharedDurationOnlyEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var assignees = await ResolveAssigneesAsync(input.AssigneeUserIds, cancellationToken);

        var mine = await _timeEntries.CreateDurationOnlyEntryAsync(
            new CreateDurationOnlyEntryInput
            {
                Description = input.Description,
                EntryDateUtc = input.EntryDateUtc,
                DurationSeconds = input.DurationSeconds,
                IsBillable = input.IsBillable,
                ProjectId = input.ProjectId,
                ProjectTaskId = input.ProjectTaskId,
                TagIds = input.TagIds
            },
            cancellationToken);

        return await AttachShareToOwnedEntryAsync(
            mine.Entry.Id,
            assignees,
            cancellationToken);
    }

    public async Task<CreateSharedManualEntryResult> ShareExistingEntryAsync(
        Guid entryId,
        ShareExistingEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var source = await _db.TimeEntries
            .Include(e => e.TimeEntryTags)
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken)
            ?? throw new AppException("Time entry not found.", 404);

        if (source.Mode == TimeEntryMode.Timer && source.EndedAtUtc is null)
            throw new AppException("Cannot share a running timer entry.", 409);

        if (source.StartedAtUtc is null)
            throw new AppException("This entry cannot be shared.", 400);

        var isAuthorOwnedConfirmed = source.UserId == userId &&
            source.SubmittedByUserId is null &&
            source.Status == TimeEntryStatus.Confirmed;
        var isParticipantConfirmed = source.UserId == userId &&
            source.Status == TimeEntryStatus.Confirmed;
        var isSubmitterShare = source.SubmittedByUserId == userId;

        if (!isParticipantConfirmed && !isSubmitterShare)
            throw new AppException("You cannot share this time entry.", 403);

        List<TimeEntry> existingShareRows = [];
        if (source.ShareGroupId is Guid existingGroupId)
        {
            existingShareRows = await LoadShareGroupRowsAsync(
                existingGroupId, cancellationToken, tracked: true);
        }

        var existingAssigneeIds = existingShareRows.Select(e => e.UserId).ToHashSet();
        var newAssigneeIds = input.AssigneeUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Where(id => id != userId && !existingAssigneeIds.Contains(id))
            .ToList();

        if (newAssigneeIds.Count == 0)
            throw new AppException("All selected teammates are already on this entry.", 400);

        var assignees = await ResolveAssigneesAsync(newAssigneeIds, cancellationToken);
        var timingSource = source;
        // Prefer submitter's own confirmed row timing when sharing from a sibling row.
        if (!isAuthorOwnedConfirmed && source.ShareGroupId is Guid groupId)
        {
            var owned = await _db.TimeEntries
                .Include(e => e.TimeEntryTags)
                .FirstOrDefaultAsync(
                    e => e.ShareGroupId == groupId &&
                         e.UserId == userId &&
                         e.SubmittedByUserId == null,
                    cancellationToken);
            if (owned is not null)
                timingSource = owned;
        }

        if (timingSource.Mode != TimeEntryMode.DurationOnly && timingSource.EndedAtUtc is null)
            throw new AppException("This entry cannot be shared.", 400);

        string? overlapMessage = null;
        if (timingSource.Mode != TimeEntryMode.DurationOnly && timingSource.EndedAtUtc is not null)
        {
            overlapMessage = await FindAssigneeOverlapMessageAsync(
                assignees,
                timingSource.StartedAtUtc!.Value,
                timingSource.EndedAtUtc.Value,
                cancellationToken);
            if (overlapMessage is not null)
                throw new AppException(overlapMessage, 409);
        }

        var shareGroupId = source.ShareGroupId
            ?? existingShareRows.FirstOrDefault()?.ShareGroupId
            ?? Guid.NewGuid();

        var now = DateTime.UtcNow;
        if (isParticipantConfirmed)
        {
            source.ShareGroupId = shareGroupId;
            source.UpdatedAtUtc = now;
        }

        foreach (var row in existingShareRows.Where(row => row.ShareGroupId != shareGroupId))
        {
            row.ShareGroupId = shareGroupId;
            row.UpdatedAtUtc = now;
        }

        return await AddPendingClonesAndNotifyAsync(
            ownedEntryForResponse: isAuthorOwnedConfirmed ? source : null,
            associationSource: timingSource,
            assignees,
            timingSource.Description,
            timingSource.Mode,
            timingSource.StartedAtUtc!.Value,
            timingSource.EndedAtUtc,
            timingSource.DurationSeconds,
            timingSource.IsBillable,
            shareGroupId,
            cancellationToken);
    }

    private async Task<CreateSharedManualEntryResult> AttachShareToOwnedEntryAsync(
        Guid ownedEntryId,
        IReadOnlyList<User> assignees,
        CancellationToken cancellationToken)
    {
        var ownedEntry = await _db.TimeEntries
            .Include(e => e.TimeEntryTags)
            .Include(e => e.Project)
            .Include(e => e.ProjectTask)
            .FirstAsync(e => e.Id == ownedEntryId, cancellationToken);

        var shareGroupId = ownedEntry.ShareGroupId ?? Guid.NewGuid();
        ownedEntry.ShareGroupId = shareGroupId;
        ownedEntry.UpdatedAtUtc = DateTime.UtcNow;

        if (ownedEntry.StartedAtUtc is null)
            throw new AppException("This entry cannot be shared.", 400);

        return await AddPendingClonesAndNotifyAsync(
            ownedEntry,
            associationSource: ownedEntry,
            assignees,
            ownedEntry.Description,
            ownedEntry.Mode,
            ownedEntry.StartedAtUtc.Value,
            ownedEntry.EndedAtUtc,
            ownedEntry.DurationSeconds,
            ownedEntry.IsBillable,
            shareGroupId,
            cancellationToken);
    }


    private async Task<CreateSharedManualEntryResult> AddPendingClonesAndNotifyAsync(
        TimeEntry? ownedEntryForResponse,
        TimeEntry associationSource,
        IReadOnlyList<User> assignees,
        string? description,
        TimeEntryMode mode,
        DateTime startedAtUtc,
        DateTime? endedAtUtc,
        int durationSeconds,
        bool isBillable,
        Guid shareGroupId,
        CancellationToken cancellationToken)
    {
        var submitterId = _currentUser.UserId;
        foreach (var assignee in assignees)
            await _entryGuard.EnsureEditableAsync(assignee.Id, startedAtUtc, cancellationToken);

        var submitter = await _db.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == submitterId, cancellationToken);

        var now = DateTime.UtcNow;
        var submitterName = submitter.DisplayName?.Trim() ?? submitter.Email;
        var assigneeById = assignees.ToDictionary(a => a.Id);
        var pendingEntries = new List<TimeEntry>();

        foreach (var assignee in assignees.OrderBy(a => a.DisplayName ?? a.Email))
        {
            var entry = new TimeEntry
            {
                UserId = assignee.Id,
                Description = TimeEntryHelpers.NormalizeDescription(description),
                IsBillable = isBillable,
                Mode = mode,
                Status = TimeEntryStatus.Pending,
                SubmittedByUserId = submitterId,
                ShareGroupId = shareGroupId,
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc,
                DurationSeconds = durationSeconds,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _associations.CopyAssociations(associationSource, entry);
            _db.TimeEntries.Add(entry);
            pendingEntries.Add(entry);
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (ownedEntryForResponse is not null)
            ownedEntryForResponse.User = submitter;

        foreach (var entry in pendingEntries)
        {
            entry.User = assigneeById[entry.UserId];
            entry.SubmittedByUser = submitter;
        }

        _emailNotifier.QueueShareNotificationEmails(pendingEntries, assigneeById, submitterName);

        var groupRows = new List<TimeEntry>();
        if (ownedEntryForResponse is not null)
            groupRows.Add(ownedEntryForResponse);
        groupRows.AddRange(pendingEntries);

        return MapSharedCreateResult(
            ownedEntryForResponse,
            pendingEntries,
            assigneeById,
            submitterName,
            new Dictionary<Guid, List<TimeEntry>> { [shareGroupId] = groupRows });
    }

    private async Task<string?> FindAssigneeOverlapMessageAsync(
        IReadOnlyList<User> assignees,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        CancellationToken cancellationToken)
    {
        foreach (var assignee in assignees)
        {
            var message = await FindOverlapMessageAsync(
                assignee.Id,
                startedAtUtc,
                endedAtUtc,
                excludeEntryId: null,
                cancellationToken);
            if (message is not null)
                return message;
        }

        return null;
    }

    private async Task<List<User>> ResolveAssigneesAsync(
        IReadOnlyList<Guid> assigneeUserIds,
        CancellationToken cancellationToken)
    {
        var distinctAssigneeIds = assigneeUserIds.Distinct().ToList();
        if (distinctAssigneeIds.Count == 0)
            throw new AppException("At least one teammate is required.", 400);

        var submitterId = _currentUser.UserId;
        if (distinctAssigneeIds.Contains(submitterId))
            throw new AppException("You cannot share a time entry with yourself.", 400);

        var assignees = await _db.Users
            .AsNoTracking()
            .Where(u => distinctAssigneeIds.Contains(u.Id) && u.Status == UserStatus.Active)
            .ToListAsync(cancellationToken);

        if (assignees.Count != distinctAssigneeIds.Count)
            throw new AppException("One or more teammates were not found.", 404);

        return assignees;
    }

    private static CreateSharedManualEntryResult MapSharedCreateResult(
        TimeEntry? ownedEntry,
        IReadOnlyList<TimeEntry> pendingEntries,
        IReadOnlyDictionary<Guid, User> assigneeById,
        string submitterName,
        IReadOnlyDictionary<Guid, List<TimeEntry>> shareGroups)
    {
        var resultEntries = new List<TimeEntryDto>();

        if (ownedEntry is not null)
        {
            resultEntries.Add(TimeEntryMapping.MapEntity(
                ownedEntry,
                submittedByDisplayName: null,
                assigneeDisplayName: submitterName,
                shareGroups));
        }

        resultEntries.AddRange(pendingEntries.Select(e =>
        {
            var assignee = assigneeById[e.UserId];
            return TimeEntryMapping.MapEntity(
                e,
                submitterName,
                assignee.DisplayName?.Trim() ?? assignee.Email,
                shareGroups);
        }));

        return new CreateSharedManualEntryResult
        {
            Entries = resultEntries
        };
    }

    private async Task<List<TimeEntry>> LoadShareGroupRowsAsync(
        Guid shareGroupId,
        CancellationToken cancellationToken,
        bool tracked = false)
    {
        var query = tracked
            ? _db.TimeEntries.AsQueryable()
            : _db.TimeEntries.AsNoTracking();

        return await query
            .Where(e => e.ShareGroupId == shareGroupId)
            .ToListAsync(cancellationToken);
    }

private async Task<string?> FindOverlapMessageAsync(
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
            return $"This entry overlaps with: {string.Join(", ", labels)}.";

        return "This entry overlaps with an existing time entry.";
    }
}

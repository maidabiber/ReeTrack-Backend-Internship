using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.TimeEntries;

public class SharedTimeEntryApprovalService : ISharedTimeEntryApprovalService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILockedPeriodService _lockedPeriod;

    public SharedTimeEntryApprovalService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILockedPeriodService lockedPeriod)
    {
        _db = db;
        _currentUser = currentUser;
        _lockedPeriod = lockedPeriod;
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Include(e => e.SubmittedByUser)
            .Include(e => e.User)
            .Where(e => e.UserId == userId && e.Status == TimeEntryStatus.Pending)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var shareGroups = await LoadShareGroupsAsync(entries, cancellationToken);

        return entries
            .Select(e => TimeEntryMapping.MapEntity(
                e,
                e.SubmittedByUser?.DisplayName ?? e.SubmittedByUser?.Email,
                e.User.DisplayName ?? e.User.Email,
                shareGroups))
            .ToList();
    }

    public async Task<UpdateTimeEntryResult> UpdatePendingEntryAsync(
        Guid entryId,
        UpdatePendingEntryInput input,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await _db.TimeEntries
            .Include(e => e.SubmittedByUser)
            .Include(e => e.User)
            .FirstOrDefaultAsync(
                e => e.Id == entryId && e.UserId == userId && e.Status == TimeEntryStatus.Pending,
                cancellationToken)
            ?? throw new AppException("Pending time entry not found.", 404);

        await ApplyTimedEntryUpdateAsync(
            entry,
            input,
            checkPreviousPeriodLock: false,
            cancellationToken);

        return new UpdateTimeEntryResult
        {
            Entry = TimeEntryMapping.MapEntity(
                entry,
                entry.SubmittedByUser?.DisplayName ?? entry.SubmittedByUser?.Email,
                entry.User.DisplayName ?? entry.User.Email)
        };
    }

    public async Task<TimeEntryDto> ApprovePendingEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var entry = await _db.TimeEntries
            .Include(e => e.SubmittedByUser)
            .Include(e => e.User)
            .FirstOrDefaultAsync(
                e => e.Id == entryId && e.UserId == userId && e.Status == TimeEntryStatus.Pending,
                cancellationToken)
            ?? throw new AppException("Pending time entry not found.", 404);

        if (entry.StartedAtUtc is not null)
            await _lockedPeriod.EnsureEntryEditableAsync(entry.StartedAtUtc.Value, cancellationToken);

        entry.Status = TimeEntryStatus.Confirmed;
        entry.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return TimeEntryMapping.MapEntity(
            entry,
            entry.SubmittedByUser?.DisplayName ?? entry.SubmittedByUser?.Email,
            entry.User.DisplayName ?? entry.User.Email);
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
        UpdatePendingEntryInput input,
        bool checkPreviousPeriodLock,
        CancellationToken cancellationToken)
    {
        if (checkPreviousPeriodLock && entry.StartedAtUtc is not null)
            await _lockedPeriod.EnsureEntryEditableAsync(entry.StartedAtUtc.Value, cancellationToken);

        TimeEntryHelpers.ValidateManualRange(input.StartedAtUtc, input.EndedAtUtc);
        await _lockedPeriod.EnsureEntryEditableAsync(input.StartedAtUtc, cancellationToken);

        var durationSeconds = (int)(input.EndedAtUtc - input.StartedAtUtc).TotalSeconds;
        await EnsureNoOverlapAsync(
            entry.UserId,
            input.StartedAtUtc,
            input.EndedAtUtc,
            excludeEntryId: entry.Id,
            cancellationToken);

        entry.Description = TimeEntryHelpers.NormalizeDescription(input.Description);
        entry.IsBillable = input.IsBillable;
        entry.StartedAtUtc = input.StartedAtUtc;
        entry.EndedAtUtc = input.EndedAtUtc;
        entry.DurationSeconds = durationSeconds;
        entry.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
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
            throw new AppException($"This entry overlaps with: {string.Join(", ", labels)}.", 409);

        throw new AppException("This entry overlaps with an existing time entry.", 409);
    }

}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.TimeEntries;

public class TimeEntryService : ITimeEntryService
{
    private const int MaxDurationSeconds = 24 * 60 * 60;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILockedPeriodService _lockedPeriod;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<TimeEntryService> _logger;
    private readonly string _frontendOrigin;
    private readonly string _appName;

    public TimeEntryService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILockedPeriodService lockedPeriod,
        IEmailSender emailSender,
        ILogger<TimeEntryService> logger,
        IConfiguration configuration,
        IOptions<AppOptions> appOptions)
    {
        _db = db;
        _currentUser = currentUser;
        _lockedPeriod = lockedPeriod;
        _emailSender = emailSender;
        _logger = logger;
        _frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
        _appName = appOptions.Value.Name;
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

    public async Task<CreateSharedManualEntryResult> StopSharedTimerAsync(
        IReadOnlyList<Guid> assigneeUserIds,
        string? description = null,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var running = await FindRunningTimerAsync(userId, cancellationToken, tracked: true)
            ?? throw new AppException("No timer is currently running.", 404);

        var now = DateTime.UtcNow;
        var startedAtUtc = running.StartedAtUtc!.Value;
        var endedAtUtc = now;
        var finalDescription = description is not null
            ? NormalizeDescription(description)
            : running.Description;

        return await CreateSharedEntriesAsync(
            assigneeUserIds,
            finalDescription,
            startedAtUtc,
            endedAtUtc,
            running.IsBillable,
            TimeEntryMode.Timer,
            confirmOverlap,
            entryToRemove: running,
            cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.SubmittedByUser)
            .Where(e =>
                (e.Mode == TimeEntryMode.DurationOnly &&
                 e.DurationSeconds > 0 &&
                 ((e.UserId == userId && e.Status == TimeEntryStatus.Confirmed) ||
                  (e.UserId == userId && e.Status == TimeEntryStatus.Pending) ||
                  (e.SubmittedByUserId == userId && e.Status == TimeEntryStatus.Pending) ||
                  (e.SubmittedByUserId == userId && e.Status == TimeEntryStatus.Confirmed))) ||
                (e.EndedAtUtc != null &&
                 ((e.UserId == userId && e.Status == TimeEntryStatus.Confirmed) ||
                  (e.UserId == userId && e.Status == TimeEntryStatus.Pending) ||
                  (e.SubmittedByUserId == userId && e.Status == TimeEntryStatus.Pending) ||
                  (e.SubmittedByUserId == userId && e.Status == TimeEntryStatus.Confirmed))))
            .OrderByDescending(e => e.StartedAtUtc ?? e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var shareGroups = await LoadShareGroupsAsync(entries, cancellationToken);

        return entries
            .Select(e => MapTimeEntry(
                e,
                e.SubmittedByUser?.DisplayName ?? e.SubmittedByUser?.Email,
                e.User.DisplayName ?? e.User.Email,
                shareGroups))
            .ToList();
    }

    public async Task<CreateSharedManualEntryResult> CreateSharedManualEntryAsync(
        IReadOnlyList<Guid> assigneeUserIds,
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable = true,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default)
    {
        return await CreateSharedEntriesAsync(
            assigneeUserIds,
            description,
            startedAtUtc,
            endedAtUtc,
            isBillable,
            TimeEntryMode.Manual,
            confirmOverlap,
            entryToRemove: null,
            cancellationToken);
    }

    public async Task<CreateSharedManualEntryResult> ShareExistingEntryAsync(
        Guid entryId,
        IReadOnlyList<Guid> assigneeUserIds,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var source = await _db.TimeEntries
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken)
            ?? throw new AppException("Time entry not found.", 404);

        if (source.EndedAtUtc is null)
            throw new AppException("Cannot share a running timer entry.", 409);

        if (source.StartedAtUtc is null)
            throw new AppException("This entry cannot be shared.", 400);

        var isOwnConfirmedEntry = source.UserId == userId &&
            source.SubmittedByUserId is null &&
            source.Status == TimeEntryStatus.Confirmed;
        var isSubmitterShare = source.SubmittedByUserId == userId;

        if (!isOwnConfirmedEntry && !isSubmitterShare)
            throw new AppException("You cannot share this time entry.", 403);

        TimeEntry? entryToRemove = null;
        List<TimeEntry> existingShareRows = [];

        if (isOwnConfirmedEntry)
        {
            entryToRemove = source;
        }
        else
        {
            existingShareRows = await LoadSubmitterShareRowsAsync(
                source,
                userId,
                cancellationToken,
                tracked: true);
        }

        var existingAssigneeIds = existingShareRows.Select(e => e.UserId).ToHashSet();
        var newAssigneeIds = assigneeUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Where(id => id != userId && !existingAssigneeIds.Contains(id))
            .ToList();

        if (newAssigneeIds.Count == 0)
            throw new AppException("All selected teammates are already on this entry.", 400);

        var timingSource = entryToRemove ?? source;
        var shareGroupId = source.ShareGroupId ?? existingShareRows.FirstOrDefault()?.ShareGroupId;
        var totalAssignees = existingShareRows.Count + newAssigneeIds.Count;

        if (totalAssignees > 1 && shareGroupId is null)
        {
            shareGroupId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            foreach (var row in existingShareRows)
            {
                row.ShareGroupId = shareGroupId;
                row.UpdatedAtUtc = now;
            }
        }

        return await CreateSharedEntriesAsync(
            newAssigneeIds,
            timingSource.Description,
            timingSource.StartedAtUtc!.Value,
            timingSource.EndedAtUtc!.Value,
            timingSource.IsBillable,
            timingSource.Mode,
            confirmOverlap,
            entryToRemove,
            cancellationToken,
            forcedShareGroupId: shareGroupId);
    }

    private async Task<CreateSharedManualEntryResult> CreateSharedEntriesAsync(
        IReadOnlyList<Guid> assigneeUserIds,
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable,
        TimeEntryMode mode,
        bool confirmOverlap,
        TimeEntry? entryToRemove,
        CancellationToken cancellationToken,
        Guid? forcedShareGroupId = null)
    {
        var distinctAssigneeIds = assigneeUserIds.Distinct().ToList();
        if (distinctAssigneeIds.Count == 0)
            throw new AppException("At least one teammate is required.", 400);

        var submitterId = RequireUserId();

        if (distinctAssigneeIds.Contains(submitterId))
            throw new AppException("You cannot share a time entry with yourself.", 400);

        var assignees = await _db.Users
            .AsNoTracking()
            .Where(u => distinctAssigneeIds.Contains(u.Id) && u.Status == UserStatus.Active)
            .ToListAsync(cancellationToken);

        if (assignees.Count != distinctAssigneeIds.Count)
            throw new AppException("One or more teammates were not found.", 404);

        if (mode == TimeEntryMode.Manual)
            ValidateManualRange(startedAtUtc, endedAtUtc);

        await _lockedPeriod.EnsureEntryEditableAsync(startedAtUtc, cancellationToken);

        var durationSeconds = mode == TimeEntryMode.Timer
            ? (int)Math.Max(0, (endedAtUtc - startedAtUtc).TotalSeconds)
            : (int)(endedAtUtc - startedAtUtc).TotalSeconds;
        string? overlapWarning = null;

        foreach (var assigneeId in distinctAssigneeIds)
        {
            var warning = await BuildOverlapWarningAsync(
                assigneeId,
                startedAtUtc,
                endedAtUtc,
                excludeEntryId: null,
                cancellationToken);

            overlapWarning ??= warning;
        }

        if (overlapWarning is not null && !confirmOverlap)
            throw new AppException(overlapWarning, 409);

        var submitter = await _db.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == submitterId, cancellationToken);

        var shareGroupId = forcedShareGroupId ?? (distinctAssigneeIds.Count > 1 ? Guid.NewGuid() : (Guid?)null);
        var now = DateTime.UtcNow;
        var normalizedDescription = NormalizeDescription(description);
        var reviewUrl = $"{_frontendOrigin.TrimEnd('/')}/approvals";
        var submitterName = submitter.DisplayName?.Trim() ?? submitter.Email;
        var createdEntries = new List<TimeEntry>();

        var assigneeById = assignees.ToDictionary(a => a.Id);

        if (entryToRemove is not null)
            _db.TimeEntries.Remove(entryToRemove);

        foreach (var assignee in assignees.OrderBy(a => a.DisplayName ?? a.Email))
        {
            var entry = new TimeEntry
            {
                UserId = assignee.Id,
                SubmittedByUserId = submitterId,
                ShareGroupId = shareGroupId,
                Description = normalizedDescription,
                IsBillable = isBillable,
                Mode = mode,
                Status = TimeEntryStatus.Pending,
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc,
                DurationSeconds = durationSeconds,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            _db.TimeEntries.Add(entry);
            createdEntries.Add(entry);
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var entry in createdEntries)
        {
            entry.User = assigneeById[entry.UserId];
            entry.SubmittedByUser = submitter;
        }

        QueueShareNotificationEmails(createdEntries, assigneeById, submitterName, reviewUrl);

        var shareGroups = shareGroupId is Guid groupId
            ? new Dictionary<Guid, List<TimeEntry>> { [groupId] = createdEntries }
            : new Dictionary<Guid, List<TimeEntry>>();

        return new CreateSharedManualEntryResult
        {
            Entries = createdEntries
                .Select(e =>
                {
                    var assignee = assigneeById[e.UserId];
                    return MapTimeEntry(
                        e,
                        submitterName,
                        assignee.DisplayName?.Trim() ?? assignee.Email,
                        shareGroups);
                })
                .ToList(),
            OverlapWarning = confirmOverlap ? overlapWarning : null
        };
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Include(e => e.SubmittedByUser)
            .Include(e => e.User)
            .Where(e => e.UserId == userId && e.Status == TimeEntryStatus.Pending)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var shareGroups = await LoadShareGroupsAsync(entries, cancellationToken);

        return entries
            .Select(e => MapTimeEntry(
                e,
                e.SubmittedByUser?.DisplayName ?? e.SubmittedByUser?.Email,
                e.User.DisplayName ?? e.User.Email,
                shareGroups))
            .ToList();
    }

    public async Task<UpdateTimeEntryResult> UpdatePendingEntryAsync(
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
            .Include(e => e.SubmittedByUser)
            .Include(e => e.User)
            .FirstOrDefaultAsync(
                e => e.Id == entryId && e.UserId == userId && e.Status == TimeEntryStatus.Pending,
                cancellationToken)
            ?? throw new AppException("Pending time entry not found.", 404);

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
            Entry = MapTimeEntry(
                entry,
                entry.SubmittedByUser?.DisplayName ?? entry.SubmittedByUser?.Email,
                entry.User.DisplayName ?? entry.User.Email),
            OverlapWarning = confirmOverlap ? overlapWarning : null
        };
    }

    public async Task<TimeEntryDto> ApprovePendingEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
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

        return MapTimeEntry(
            entry,
            entry.SubmittedByUser?.DisplayName ?? entry.SubmittedByUser?.Email,
            entry.User.DisplayName ?? entry.User.Email);
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

    public async Task<CreateManualEntryResult> CreateDurationOnlyEntryAsync(
        string? description,
        DateTime entryDateUtc,
        int durationSeconds,
        bool isBillable = true,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        ValidateDurationOnly(durationSeconds);
        var normalizedEntryDateUtc = NormalizeEntryDateUtc(entryDateUtc);

        var now = DateTime.UtcNow;
        await _lockedPeriod.EnsureEntryEditableAsync(normalizedEntryDateUtc, cancellationToken);

        var entry = new TimeEntry
        {
            UserId = userId,
            Description = NormalizeDescription(description),
            IsBillable = isBillable,
            Mode = TimeEntryMode.DurationOnly,
            StartedAtUtc = normalizedEntryDateUtc,
            EndedAtUtc = null,
            DurationSeconds = durationSeconds,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateManualEntryResult
        {
            Entry = MapTimeEntry(entry),
            OverlapWarning = null
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

        if (entry.Mode == TimeEntryMode.DurationOnly)
            throw new AppException("Duration-only entries must be updated without start/end times.", 409);

        if (entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null)
            throw new AppException("Cannot edit a running timer entry.", 409);

        if (entry.Status == TimeEntryStatus.Pending)
            throw new AppException("Pending entries must be reviewed on the Approvals page.", 409);

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

    public async Task<UpdateTimeEntryResult> UpdateDurationOnlyEntryAsync(
        Guid entryId,
        string? description,
        DateTime entryDateUtc,
        int durationSeconds,
        bool isBillable,
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

        ValidateDurationOnly(durationSeconds);
        var normalizedEntryDateUtc = NormalizeEntryDateUtc(entryDateUtc);
        await _lockedPeriod.EnsureEntryEditableAsync(normalizedEntryDateUtc, cancellationToken);

        var now = DateTime.UtcNow;
        entry.Description = NormalizeDescription(description);
        entry.IsBillable = isBillable;
        entry.DurationSeconds = durationSeconds;
        entry.StartedAtUtc = normalizedEntryDateUtc;
        entry.EndedAtUtc = null;
        entry.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateTimeEntryResult
        {
            Entry = MapTimeEntry(entry),
            OverlapWarning = null
        };
    }

    private void QueueShareNotificationEmails(
        IReadOnlyList<TimeEntry> createdEntries,
        IReadOnlyDictionary<Guid, User> assigneeById,
        string submitterName,
        string reviewUrl)
    {
        foreach (var entry in createdEntries)
        {
            var assignee = assigneeById[entry.UserId];
            var assigneeName = assignee.DisplayName?.Trim() ?? assignee.Email;

            _ = SendShareNotificationEmailAsync(
                entry.Id,
                assignee.Email,
                assigneeName,
                submitterName,
                entry.Description,
                reviewUrl);
        }
    }

    private async Task SendShareNotificationEmailAsync(
        Guid entryId,
        string assigneeEmail,
        string assigneeName,
        string submitterName,
        string? description,
        string reviewUrl)
    {
        try
        {
            await _emailSender.SendTimeEntryMentionEmailAsync(
                assigneeEmail,
                assigneeName,
                submitterName,
                description,
                reviewUrl,
                _appName,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Saved shared time entry {EntryId} for {AssigneeEmail}, but mention email could not be sent.",
                entryId,
                assigneeEmail);
        }
    }

    private static void ValidateManualRange(DateTime startedAtUtc, DateTime endedAtUtc)
    {
        if (endedAtUtc <= startedAtUtc)
            throw new AppException("End time must be after start time.");

        var durationSeconds = (endedAtUtc - startedAtUtc).TotalSeconds;
        if (durationSeconds > MaxDurationSeconds)
            throw new AppException("Duration cannot exceed 24 hours.");
    }

    private static void ValidateDurationOnly(int durationSeconds)
    {
        if (durationSeconds <= 0)
            throw new AppException("Duration must be greater than zero.", 400);

        if (durationSeconds > MaxDurationSeconds)
            throw new AppException("Duration cannot exceed 24 hours.", 400);
    }

    private static DateTime NormalizeEntryDateUtc(DateTime entryDateUtc)
    {
        var utc = entryDateUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(entryDateUtc, DateTimeKind.Utc)
            : entryDateUtc.ToUniversalTime();

        return new DateTime(utc.Year, utc.Month, utc.Day, 12, 0, 0, DateTimeKind.Utc);
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

        return entries.Select(e => MapTimeEntry(e)).ToList();
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

    private async Task<List<TimeEntry>> LoadSubmitterShareRowsAsync(
        TimeEntry source,
        Guid submitterId,
        CancellationToken cancellationToken,
        bool tracked = false)
    {
        var query = tracked
            ? _db.TimeEntries.AsQueryable()
            : _db.TimeEntries.AsNoTracking();

        if (source.ShareGroupId is Guid groupId)
        {
            return await query
                .Where(e => e.ShareGroupId == groupId && e.SubmittedByUserId == submitterId)
                .ToListAsync(cancellationToken);
        }

        if (source.StartedAtUtc is null || source.EndedAtUtc is null)
            return [source];

        return await query
            .Where(e =>
                e.SubmittedByUserId == submitterId &&
                e.StartedAtUtc == source.StartedAtUtc &&
                e.EndedAtUtc == source.EndedAtUtc)
            .ToListAsync(cancellationToken);
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

    private static IReadOnlyList<TimeEntryParticipantDto> BuildParticipants(
        TimeEntry entry,
        IReadOnlyDictionary<Guid, List<TimeEntry>> shareGroups)
    {
        var participants = new List<TimeEntryParticipantDto>();
        var seenUserIds = new HashSet<Guid>();

        void AddParticipant(Guid userId, string displayName, string email, string role)
        {
            if (!seenUserIds.Add(userId))
                return;

            participants.Add(new TimeEntryParticipantDto
            {
                UserId = userId,
                DisplayName = displayName,
                Email = email,
                Role = role
            });
        }

        if (entry.SubmittedByUser is { } submitter && entry.SubmittedByUserId is Guid submitterId)
        {
            AddParticipant(
                submitterId,
                submitter.DisplayName?.Trim() ?? submitter.Email,
                submitter.Email,
                "Submitter");
        }

        if (entry.ShareGroupId is Guid groupId && shareGroups.TryGetValue(groupId, out var siblings))
        {
            foreach (var sibling in siblings)
            {
                AddParticipant(
                    sibling.UserId,
                    sibling.User.DisplayName?.Trim() ?? sibling.User.Email,
                    sibling.User.Email,
                    "Assignee");
            }
        }
        else if (entry.SubmittedByUserId is not null)
        {
            AddParticipant(
                entry.UserId,
                entry.User.DisplayName?.Trim() ?? entry.User.Email,
                entry.User.Email,
                "Assignee");
        }

        return participants;
    }

    internal static TimeEntryDto MapTimeEntry(
        TimeEntry entry,
        string? submittedByDisplayName = null,
        string? assigneeDisplayName = null,
        IReadOnlyDictionary<Guid, List<TimeEntry>>? shareGroups = null) =>
        new()
        {
            Id = entry.Id,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            Mode = entry.Mode.ToString(),
            StartedAtUtc = entry.StartedAtUtc,
            EndedAtUtc = entry.EndedAtUtc,
            DurationSeconds = entry.DurationSeconds,
            IsRunning = entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null,
            Status = entry.Status.ToString(),
            SubmittedByUserId = entry.SubmittedByUserId,
            SubmittedByDisplayName = submittedByDisplayName,
            AssigneeUserId = entry.UserId,
            AssigneeDisplayName = assigneeDisplayName,
            ShareGroupId = entry.ShareGroupId,
            Participants = shareGroups is null ? [] : BuildParticipants(entry, shareGroups)
        };
}

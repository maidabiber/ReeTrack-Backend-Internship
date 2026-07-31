using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Notifications;
using ReeTrack.Application.Notifications.Events;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Timesheets;

public class TimesheetReviewService : ITimesheetReviewService
{
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDomainEventPublisher _eventPublisher;

    public TimesheetReviewService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDomainEventPublisher eventPublisher)
    {
        _db = db;
        _currentUser = currentUser;
        _eventPublisher = eventPublisher;
    }

    public async Task<PagedResult<AdminTimesheetListItemDto>> ListAsync(
        TimesheetStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Timesheets
            .AsNoTracking()
            .Include(t => t.User)
            .Where(t => status == null || t.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);
        var timesheets = await query
            .OrderBy(t => t.SubmittedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalsByWeek = await LoadWeekTotalsAsync(
            timesheets.Select(t => (t.UserId, t.WeekStartDate)).ToList(),
            cancellationToken);

        var items = timesheets
            .Select(t =>
            {
                // Missing key yields the default tuple (0, 0) for weeks with no entries.
                var totals = totalsByWeek.GetValueOrDefault((t.UserId, t.WeekStartDate));
                return new AdminTimesheetListItemDto
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    UserDisplayName = t.User.DisplayName,
                    UserEmail = t.User.Email,
                    WeekStartDate = t.WeekStartDate,
                    Status = t.Status.ToString(),
                    SubmittedAtUtc = t.SubmittedAtUtc,
                    TotalSeconds = totals.TotalSeconds,
                    EntryCount = totals.EntryCount
                };
            })
            .ToList();

        return new PagedResult<AdminTimesheetListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminTimesheetDetailDto> GetAsync(
        Guid timesheetId,
        CancellationToken cancellationToken = default)
    {
        var timesheet = await _db.Timesheets
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.ReviewedByUser)
            .FirstOrDefaultAsync(t => t.Id == timesheetId, cancellationToken)
            ?? throw AppErrors.NotFound("Timesheet");

        var entries = await TimesheetQueries.WeekEntriesAsync(
            _db, timesheet.UserId, timesheet.WeekStartDate, cancellationToken);

        return new AdminTimesheetDetailDto
        {
            Timesheet = TimesheetMapping.MapTimesheet(timesheet),
            UserDisplayName = timesheet.User.DisplayName,
            UserEmail = timesheet.User.Email,
            Entries = entries.Select(TimesheetMapping.MapEntry).ToList(),
            TotalSeconds = entries.Sum(e => (long)e.DurationSeconds),
            BillableSeconds = entries.Where(e => e.IsBillable).Sum(e => (long)e.DurationSeconds)
        };
    }

    public Task<TimesheetDto> ApproveAsync(
        Guid timesheetId,
        string? comment,
        CancellationToken cancellationToken = default) =>
        ReviewAsync(timesheetId, TimesheetStatus.Approved, comment, cancellationToken);

    public Task<TimesheetDto> RejectAsync(
        Guid timesheetId,
        string? comment,
        CancellationToken cancellationToken = default) =>
        ReviewAsync(timesheetId, TimesheetStatus.Rejected, comment, cancellationToken);

    private async Task<TimesheetDto> ReviewAsync(
        Guid timesheetId,
        TimesheetStatus decision,
        string? comment,
        CancellationToken cancellationToken)
    {
        var reviewerId = _currentUser.UserId;
        var timesheet = await _db.Timesheets
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == timesheetId, cancellationToken)
            ?? throw AppErrors.NotFound("Timesheet");

        // Approve is only valid on a fresh submission; a send-back (reject) is also
        // allowed on an already-approved sheet so an admin can reopen it for fixes.
        var allowed = decision == TimesheetStatus.Approved
            ? timesheet.Status == TimesheetStatus.Submitted
            : timesheet.Status is TimesheetStatus.Submitted or TimesheetStatus.Approved;

        if (!allowed)
            throw AppErrors.Conflict(
                decision == TimesheetStatus.Approved
                    ? "Only a submitted timesheet can be approved."
                    : "This timesheet can no longer be sent back.");

        // Load the reviewer tracked (not AsNoTracking): when an admin reviews
        // their own timesheet the owner is already tracked via Include(t => t.User),
        // so EF's identity map hands back that same instance instead of attaching a
        // second User with the same key -- which the audit interceptor's change scan
        // rejects as an identity conflict when the row is saved.
        var reviewer = await _db.Users
            .FirstAsync(u => u.Id == reviewerId, cancellationToken);

        timesheet.Status = decision;
        timesheet.ReviewedByUserId = reviewerId;
        timesheet.ReviewedAtUtc = DateTime.UtcNow;
        timesheet.ReviewComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        timesheet.ReviewedByUser = reviewer;

        await _db.SaveChangesAsync(cancellationToken);

        _ = _eventPublisher.PublishAsync(new TimesheetDecisionNotification
        {
            TimesheetId = timesheet.Id,
            RecipientUserId = timesheet.UserId,
            RecipientName = timesheet.User.DisplayName?.Trim() ?? timesheet.User.Email,
            ReviewerName = reviewer.DisplayName?.Trim() ?? reviewer.Email,
            WeekStartDate = timesheet.WeekStartDate,
            Approved = decision == TimesheetStatus.Approved,
            Comment = timesheet.ReviewComment
        });

        return TimesheetMapping.MapTimesheet(timesheet);
    }

    private async Task<Dictionary<(Guid UserId, DateOnly Week), (long TotalSeconds, int EntryCount)>> LoadWeekTotalsAsync(
        IReadOnlyList<(Guid UserId, DateOnly Week)> weeks,
        CancellationToken cancellationToken)
    {
        if (weeks.Count == 0)
            return [];

        var userIds = weeks.Select(w => w.UserId).Distinct().ToList();
        var rangeStartUtc = TimesheetWeek.ToUtcMidnight(weeks.Min(w => w.Week));
        var rangeEndUtc = TimesheetWeek.ToUtcMidnight(weeks.Max(w => w.Week).AddDays(7));

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(e => userIds.Contains(e.UserId) &&
                        e.StartedAtUtc >= rangeStartUtc &&
                        e.StartedAtUtc < rangeEndUtc)
            .Select(e => new { e.UserId, StartedAtUtc = e.StartedAtUtc!.Value, e.DurationSeconds })
            .ToListAsync(cancellationToken);

        var wanted = weeks.ToHashSet();
        return entries
            .GroupBy(e => (e.UserId, Week: TimesheetWeek.ToWeekStart(e.StartedAtUtc)))
            .Where(g => wanted.Contains(g.Key))
            .ToDictionary(
                g => g.Key,
                g => (g.Sum(e => (long)e.DurationSeconds), g.Count()));
    }
}

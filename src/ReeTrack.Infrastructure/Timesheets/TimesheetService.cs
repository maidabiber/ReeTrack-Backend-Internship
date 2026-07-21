using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Timesheets;

public class TimesheetService : ITimesheetService
{
    private const int MaxRecentWeeks = 26;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TimesheetService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MyWeekTimesheetDto> GetMyWeekAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        EnsureMonday(weekStart);

        var timesheet = await _db.Timesheets
            .AsNoTracking()
            .Include(t => t.ReviewedByUser)
            .FirstOrDefaultAsync(
                t => t.UserId == userId && t.WeekStartDate == weekStart,
                cancellationToken);

        var entries = await TimesheetQueries.WeekEntriesAsync(_db, userId, weekStart, cancellationToken);
        var blockers = EvaluateSubmitBlockers(timesheet, entries, weekStart);

        return new MyWeekTimesheetDto
        {
            Timesheet = timesheet is null ? null : TimesheetMapping.MapTimesheet(timesheet),
            Entries = entries.Select(TimesheetMapping.MapEntry).ToList(),
            CanSubmit = blockers.Count == 0,
            Blockers = blockers.Select(b => b.Message).ToList()
        };
    }

    public async Task<IReadOnlyList<WeekSummaryDto>> GetRecentWeeksAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        count = Math.Clamp(count, 1, MaxRecentWeeks);

        var currentWeek = TimesheetWeek.ToWeekStart(DateTime.UtcNow);
        var oldestWeek = currentWeek.AddDays(-7 * (count - 1));
        var rangeStartUtc = TimesheetWeek.ToUtcMidnight(oldestWeek);
        var rangeEndUtc = TimesheetWeek.ToUtcMidnight(currentWeek.AddDays(7));

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId &&
                        e.StartedAtUtc >= rangeStartUtc &&
                        e.StartedAtUtc < rangeEndUtc)
            .Select(e => new { StartedAtUtc = e.StartedAtUtc!.Value, e.DurationSeconds, e.IsBillable })
            .ToListAsync(cancellationToken);

        var timesheets = await _db.Timesheets
            .AsNoTracking()
            .Where(t => t.UserId == userId &&
                        t.WeekStartDate >= oldestWeek &&
                        t.WeekStartDate <= currentWeek)
            .ToListAsync(cancellationToken);

        var entriesByWeek = entries.ToLookup(e => TimesheetWeek.ToWeekStart(e.StartedAtUtc));
        var timesheetsByWeek = timesheets.ToDictionary(t => t.WeekStartDate);

        return Enumerable.Range(0, count)
            .Select(i =>
            {
                var week = currentWeek.AddDays(-7 * i);
                var timesheet = timesheetsByWeek.GetValueOrDefault(week);
                return new WeekSummaryDto
                {
                    WeekStartDate = week,
                    TotalSeconds = entriesByWeek[week].Sum(e => (long)e.DurationSeconds),
                    BillableSeconds = entriesByWeek[week]
                        .Where(e => e.IsBillable)
                        .Sum(e => (long)e.DurationSeconds),
                    Status = timesheet?.Status.ToString() ?? "None",
                    TimesheetId = timesheet?.Id
                };
            })
            .ToList();
    }

    public async Task<TimesheetDto> SubmitAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        EnsureMonday(weekStart);

        var timesheet = await _db.Timesheets
            .FirstOrDefaultAsync(
                t => t.UserId == userId && t.WeekStartDate == weekStart,
                cancellationToken);

        var entries = await TimesheetQueries.WeekEntriesAsync(_db, userId, weekStart, cancellationToken);

        var blocker = EvaluateSubmitBlockers(timesheet, entries, weekStart).FirstOrDefault();
        if (blocker is not null)
            throw new AppException(blocker.Message, blocker.StatusCode);

        var now = DateTime.UtcNow;
        if (timesheet is null)
        {
            timesheet = new Timesheet
            {
                UserId = userId,
                WeekStartDate = weekStart,
                Status = TimesheetStatus.Submitted,
                SubmittedAtUtc = now
            };
            _db.Timesheets.Add(timesheet);
        }
        else
        {
            // Resubmission after rejection reuses the row; the audit trail keeps the history.
            timesheet.Status = TimesheetStatus.Submitted;
            timesheet.SubmittedAtUtc = now;
            timesheet.ReviewedByUserId = null;
            timesheet.ReviewedAtUtc = null;
            timesheet.ReviewComment = null;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Unique (user_id, week_start_date) index lost a double-submit race.
            throw new AppException("This week's timesheet has already been submitted.", 409);
        }

        return TimesheetMapping.MapTimesheet(timesheet);
    }

    public async Task WithdrawAsync(
        Guid timesheetId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var timesheet = await _db.Timesheets
            .FirstOrDefaultAsync(
                t => t.Id == timesheetId && t.UserId == userId,
                cancellationToken)
            ?? throw new AppException("Timesheet not found.", 404);

        if (timesheet.Status != TimesheetStatus.Submitted)
            throw new AppException("Only a submitted timesheet can be withdrawn.", 409);

        // Hard delete (not soft): frees the unique week index for resubmission;
        // the deletion is still recorded in the audit trail.
        _db.Timesheets.Remove(timesheet);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private sealed record SubmitBlocker(string Message, int StatusCode);

    /// <summary>
    /// Single source of truth for why a week cannot be submitted: GetMyWeekAsync
    /// surfaces these to the UI and SubmitAsync enforces the first one.
    /// </summary>
    private static List<SubmitBlocker> EvaluateSubmitBlockers(
        Timesheet? timesheet,
        IReadOnlyList<TimeEntry> entries,
        DateOnly weekStart)
    {
        var blockers = new List<SubmitBlocker>();

        if (timesheet is not null && timesheet.Status != TimesheetStatus.Rejected)
            blockers.Add(new("This week's timesheet has already been submitted.", 409));

        if (weekStart > TimesheetWeek.ToWeekStart(DateTime.UtcNow))
            blockers.Add(new("A future week cannot be submitted.", 400));

        if (entries.Any(TimesheetMapping.IsRunning))
            blockers.Add(new("Stop your running timer before submitting this week.", 409));

        if (entries.Any(e => e.Status == TimeEntryStatus.Pending))
            blockers.Add(new("Review your pending shared entries before submitting this week.", 409));

        if (entries.Sum(e => (long)e.DurationSeconds) == 0)
            blockers.Add(new("There is no time logged in this week.", 400));

        return blockers;
    }

    private static void EnsureMonday(DateOnly weekStart)
    {
        if (weekStart.DayOfWeek != DayOfWeek.Monday)
            throw new AppException("Week start must be a Monday.", 400);
    }
}

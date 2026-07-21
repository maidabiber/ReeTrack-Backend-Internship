using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Timesheets;

internal static class TimesheetQueries
{
    /// <summary>A user's time entries in the given week, with project/client loaded.</summary>
    public static Task<List<TimeEntry>> WeekEntriesAsync(
        IApplicationDbContext db,
        Guid userId,
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        var weekStartUtc = TimesheetWeek.ToUtcMidnight(weekStart);
        var weekEndUtc = TimesheetWeek.ToUtcMidnight(weekStart.AddDays(7));

        return db.TimeEntries
            .AsNoTracking()
            .Include(e => e.Project)
            .ThenInclude(p => p!.Client)
            .Where(e => e.UserId == userId &&
                        e.StartedAtUtc >= weekStartUtc &&
                        e.StartedAtUtc < weekEndUtc)
            .OrderBy(e => e.StartedAtUtc)
            .ToListAsync(cancellationToken);
    }
}

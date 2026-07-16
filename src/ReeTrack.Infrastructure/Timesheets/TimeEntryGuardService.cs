using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Timesheets;

public class TimeEntryGuardService : ITimeEntryGuardService
{
    private readonly IApplicationDbContext _db;
    private readonly ILockedPeriodService _lockedPeriod;

    public TimeEntryGuardService(IApplicationDbContext db, ILockedPeriodService lockedPeriod)
    {
        _db = db;
        _lockedPeriod = lockedPeriod;
    }

    public async Task EnsureEditableAsync(
        Guid ownerUserId,
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await _lockedPeriod.EnsureEntryEditableAsync(startedAtUtc, cancellationToken);

        var weekStart = TimesheetWeek.ToWeekStart(startedAtUtc);
        var weekLocked = await _db.Timesheets.AnyAsync(
            t => t.UserId == ownerUserId &&
                 t.WeekStartDate == weekStart &&
                 t.Status != TimesheetStatus.Rejected,
            cancellationToken);

        if (weekLocked)
            throw new AppException("This week's timesheet has been submitted and can no longer be edited.", 409);
    }
}

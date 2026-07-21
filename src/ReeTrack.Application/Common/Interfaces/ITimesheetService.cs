using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITimesheetService
{
    Task<MyWeekTimesheetDto> GetMyWeekAsync(DateOnly weekStart, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WeekSummaryDto>> GetRecentWeeksAsync(int count, CancellationToken cancellationToken = default);

    Task<TimesheetDto> SubmitAsync(DateOnly weekStart, CancellationToken cancellationToken = default);

    Task WithdrawAsync(Guid timesheetId, CancellationToken cancellationToken = default);
}

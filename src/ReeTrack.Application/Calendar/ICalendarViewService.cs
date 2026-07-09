using ReeTrack.Application.Calendar.Models;

namespace ReeTrack.Application.Calendar;

public interface ICalendarViewService
{
    Task<CalendarViewDto> GetViewAsync(
        Guid userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);
}

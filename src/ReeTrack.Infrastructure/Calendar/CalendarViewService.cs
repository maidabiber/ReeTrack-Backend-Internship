using Microsoft.Extensions.Options;
using ReeTrack.Application.Calendar;
using ReeTrack.Application.Calendar.Models;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Calendar;

namespace ReeTrack.Infrastructure.Calendar;

public class CalendarViewService : ICalendarViewService
{
    private readonly ITimeEntryService _timeEntryService;
    private readonly ICalendarIntegrationService _calendarIntegrationService;
    private readonly CalendarSyncOptions _syncOptions;

    public CalendarViewService(
        ITimeEntryService timeEntryService,
        ICalendarIntegrationService calendarIntegrationService,
        IOptions<CalendarSyncOptions> syncOptions)
    {
        _timeEntryService = timeEntryService;
        _calendarIntegrationService = calendarIntegrationService;
        _syncOptions = syncOptions.Value;
    }

    public async Task<CalendarViewDto> GetViewAsync(
        Guid userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-_syncOptions.LookbackDays);
        var to = toUtc ?? DateTime.UtcNow.AddDays(_syncOptions.LookaheadDays);

        var timeEntries = await _timeEntryService.ListByDateRangeAsync(from, to, cancellationToken);
        var calendarEvents = await _calendarIntegrationService.GetEventsAsync(userId, from, to, cancellationToken);

        return new CalendarViewDto
        {
            TimeEntries = timeEntries,
            CalendarEvents = calendarEvents
        };
    }
}

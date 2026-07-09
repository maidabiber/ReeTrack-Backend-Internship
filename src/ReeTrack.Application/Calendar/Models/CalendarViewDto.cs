using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Integrations.Calendar.Models;

namespace ReeTrack.Application.Calendar.Models;

public sealed class CalendarViewDto
{
    public required IReadOnlyList<TimeEntryDto> TimeEntries { get; init; }
    public required IReadOnlyList<SyncedCalendarEventDto> CalendarEvents { get; init; }
}

using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Integrations.Calendar;

public interface ICalendarProviderRegistry
{
    ICalendarProvider GetProvider(CalendarProviderType providerType);
}

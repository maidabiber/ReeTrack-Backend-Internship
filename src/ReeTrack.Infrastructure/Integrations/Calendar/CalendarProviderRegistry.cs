using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Integrations.Calendar;

public class CalendarProviderRegistry : ICalendarProviderRegistry
{
    private readonly IReadOnlyDictionary<CalendarProviderType, ICalendarProvider> _providers;

    public CalendarProviderRegistry(IEnumerable<ICalendarProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.ProviderType);
    }

    public ICalendarProvider GetProvider(CalendarProviderType providerType)
    {
        if (_providers.TryGetValue(providerType, out var provider))
            return provider;

        throw new CalendarIntegrationException($"Calendar provider '{providerType}' is not supported.", 400, ErrorCode.Validation);
    }
}

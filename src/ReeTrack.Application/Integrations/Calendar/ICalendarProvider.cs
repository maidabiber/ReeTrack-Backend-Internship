using ReeTrack.Application.Integrations.Calendar.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Integrations.Calendar;

public interface ICalendarProvider
{
    CalendarProviderType ProviderType { get; }
    string BuildAuthorizationUrl(string state);
    Task<OAuthTokenSet> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<OAuthTokenSet> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExternalCalendarEvent>> FetchEventsAsync(
        string accessToken,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

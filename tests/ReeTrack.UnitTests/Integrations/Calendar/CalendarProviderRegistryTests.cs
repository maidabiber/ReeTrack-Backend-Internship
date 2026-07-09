using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Integrations.Calendar;
using ReeTrack.Infrastructure.Integrations.Calendar.Google;
using Xunit;

namespace ReeTrack.UnitTests.Integrations.Calendar;

public class CalendarProviderRegistryTests
{
    [Fact]
    public void GetProvider_ReturnsGoogleProvider()
    {
        var registry = new CalendarProviderRegistry([new FakeGoogleProvider()]);

        var provider = registry.GetProvider(CalendarProviderType.Google);

        Assert.Equal(CalendarProviderType.Google, provider.ProviderType);
    }

    [Fact]
    public void GetProvider_ThrowsForUnsupportedProvider()
    {
        var registry = new CalendarProviderRegistry([new FakeGoogleProvider()]);

        var ex = Assert.Throws<CalendarIntegrationException>(
            () => registry.GetProvider((CalendarProviderType)99));

        Assert.Equal(400, ex.StatusCode);
    }

    private sealed class FakeGoogleProvider : ICalendarProvider
    {
        public CalendarProviderType ProviderType => CalendarProviderType.Google;

        public string BuildAuthorizationUrl(string state) => $"https://example.test?state={state}";

        public Task<Application.Integrations.Calendar.Models.OAuthTokenSet> ExchangeCodeAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Application.Integrations.Calendar.Models.OAuthTokenSet> RefreshAccessTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<Application.Integrations.Calendar.Models.ExternalCalendarEvent>> FetchEventsAsync(
            string accessToken,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}

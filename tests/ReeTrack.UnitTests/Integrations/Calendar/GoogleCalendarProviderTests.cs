using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Infrastructure.Integrations.Calendar.Google;
using Xunit;

namespace ReeTrack.UnitTests.Integrations.Calendar;

public class GoogleCalendarProviderTests
{
    [Fact]
    public void BuildAuthorizationUrl_IncludesOfflineAccessAndConsent()
    {
        var provider = CreateProvider();

        var url = provider.BuildAuthorizationUrl("test-state");

        Assert.Contains("access_type=offline", url);
        Assert.Contains("prompt=consent", url);
        Assert.Contains("calendar.readonly", url);
        Assert.Contains("state=test-state", url);
        Assert.Contains("redirect_uri=", url);
    }

    private static GoogleCalendarProvider CreateProvider()
    {
        var options = Options.Create(new GoogleAuthOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            CalendarRedirectUri = "http://localhost:5173/api/integrations/calendar/google/callback"
        });

        return new GoogleCalendarProvider(new HttpClient(), options);
    }
}

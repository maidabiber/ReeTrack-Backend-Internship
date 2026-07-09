using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ReeTrack.IntegrationTests.Integrations.Calendar;

public class CalendarIntegrationsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CalendarIntegrationsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListConnections_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/integrations/calendar");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/calendar/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

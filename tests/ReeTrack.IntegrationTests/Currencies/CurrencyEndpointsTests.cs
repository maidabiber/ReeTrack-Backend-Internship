using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Api.Contracts;
using ReeTrack.Domain.Entities;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Currencies;

public class CurrencyEndpointsTests
{
    [Fact]
    public async Task List_RequiresAuthentication()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/currencies");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsSeededEuroOnly()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/currencies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CurrenciesResponse>();
        Assert.NotNull(body);
        var euro = Assert.Single(body.Items);
        Assert.Equal("EUR", euro.Code);
        Assert.Equal("Euro", euro.Name);
    }

    [Fact]
    public async Task CreateProject_UnsupportedCurrency_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            name = "USD Project",
            clientId,
            billingType = "hourly",
            currencyCode = "USD",
            hourlyRate = 100
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Guid> SeedClientAsync(ReeTrackWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = new Client { Name = "Currency Test Client" };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }
}

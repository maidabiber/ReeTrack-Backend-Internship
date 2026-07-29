using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Api.Contracts;
using ReeTrack.Domain.Constants;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Members;

public class UserHourlyRateEndpointsTests
{
    [Fact]
    public async Task SeededUser_HasInitialMinimumWageRate()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var current = await client.GetFromJsonAsync<UserHourlyRateResponse>(
            $"/api/members/{admin.Id}/hourly-rates/current");

        Assert.NotNull(current);
        Assert.Equal(UserHourlyRateDefaults.MinimumWage.Amount, current.HourlyRate);
        Assert.Equal("EUR", current.CurrencyCode);
        Assert.Null(current.ValidTo);
    }

    [Fact]
    public async Task Member_CannotAccessHourlyRates_Returns403()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, _) = await factory.SeedAdminAsync();
        var (_, memberToken) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(memberToken);

        var response = await client.GetAsync($"/api/members/{admin.Id}/hourly-rates");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_ChangeHourlyRate_ClosesPreviousWithoutGap()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var (member, _) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        DateOnly initialFrom;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var initial = await db.UserHourlyRates.SingleAsync(r => r.UserId == member.Id);
            initialFrom = initial.ValidFrom;
        }

        var validFrom = initialFrom.AddDays(10);
        var change = await client.PostAsJsonAsync($"/api/members/{member.Id}/hourly-rates", new
        {
            hourlyRate = 25.50m,
            validFrom
        });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var created = await change.Content.ReadFromJsonAsync<UserHourlyRateResponse>();
        Assert.NotNull(created);
        Assert.Equal(25.50m, created.HourlyRate);
        Assert.Equal(validFrom, created.ValidFrom);
        Assert.Null(created.ValidTo);

        var history = await client.GetFromJsonAsync<List<UserHourlyRateResponse>>(
            $"/api/members/{member.Id}/hourly-rates");
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);

        var previous = Assert.Single(history, r => r.ValidTo is not null);
        Assert.Equal(validFrom.AddDays(-1), previous.ValidTo);

        var onPreviousDay = await client.GetFromJsonAsync<UserHourlyRateResponse>(
            $"/api/members/{member.Id}/hourly-rates/current?onDate={validFrom.AddDays(-1):yyyy-MM-dd}");
        var onNewDay = await client.GetFromJsonAsync<UserHourlyRateResponse>(
            $"/api/members/{member.Id}/hourly-rates/current?onDate={validFrom:yyyy-MM-dd}");

        Assert.Equal(previous.Id, onPreviousDay!.Id);
        Assert.Equal(created.Id, onNewDay!.Id);
    }

    [Fact]
    public async Task Admin_ChangeHourlyRate_InvalidValidFrom_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var (member, _) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        DateOnly initialFrom;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            initialFrom = (await db.UserHourlyRates.SingleAsync(r => r.UserId == member.Id)).ValidFrom;
        }

        var response = await client.PostAsJsonAsync($"/api/members/{member.Id}/hourly-rates", new
        {
            hourlyRate = 30m,
            validFrom = initialFrom
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InviteCreatesUser_WithInitialHourlyRate()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var invite = await client.PostAsJsonAsync("/api/invitations", new
        {
            email = "new.hire@reetrack.test",
            roleId = 2
        });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "new.hire@reetrack.test");
            userId = user.Id;

            var rate = await db.UserHourlyRates.SingleAsync(r => r.UserId == user.Id);
            Assert.Equal(UserHourlyRateDefaults.MinimumWage.Amount, rate.Rate.Amount);
            Assert.Equal("EUR", rate.Rate.CurrencyCode);
            Assert.Null(rate.ValidTo);
        }

        var current = await client.GetFromJsonAsync<UserHourlyRateResponse>(
            $"/api/members/{userId}/hourly-rates/current");
        Assert.Equal(12.82m, current!.HourlyRate);
    }

    [Fact]
    public async Task Admin_CorrectHourlyRate_UpdatesAmountAndDates()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var (member, _) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        DateOnly initialFrom;
        Guid rateId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var initial = await db.UserHourlyRates.SingleAsync(r => r.UserId == member.Id);
            initialFrom = initial.ValidFrom;
            rateId = initial.Id;
        }

        // Use offsets from the seeded ValidFrom so this stays valid regardless of
        // calendar day (month-boundary math can make correctedTo < correctedFrom).
        var midFrom = initialFrom.AddDays(20);
        var create = await client.PostAsJsonAsync($"/api/members/{member.Id}/hourly-rates", new
        {
            hourlyRate = 20m,
            validFrom = midFrom
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var history = await client.GetFromJsonAsync<List<UserHourlyRateResponse>>(
            $"/api/members/{member.Id}/hourly-rates");
        var first = Assert.Single(history!, r => r.Id == rateId);

        var correctedFrom = initialFrom;
        var correctedTo = midFrom.AddDays(-5);
        var patch = await client.PatchAsJsonAsync(
            $"/api/members/{member.Id}/hourly-rates/{first.Id}",
            new
            {
                hourlyRate = 14.50m,
                currencyCode = "EUR",
                validFrom = correctedFrom,
                validTo = correctedTo
            });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var body = await patch.Content.ReadFromJsonAsync<UserHourlyRateResponse>();
        Assert.Equal(14.50m, body!.HourlyRate);
        Assert.Equal(correctedFrom, body.ValidFrom);
        Assert.Equal(correctedTo, body.ValidTo);

        var after = await client.GetFromJsonAsync<List<UserHourlyRateResponse>>(
            $"/api/members/{member.Id}/hourly-rates");
        Assert.Equal(2, after!.Count);
        var second = Assert.Single(after, r => r.ValidTo is null);
        Assert.Equal(correctedTo.AddDays(1), second.ValidFrom);
    }

    [Fact]
    public async Task Member_CorrectHourlyRate_Returns403()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();

        Guid rateId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            rateId = (await db.UserHourlyRates.SingleAsync(r => r.UserId == member.Id)).Id;
        }

        var client = factory.CreateAuthenticatedClient(memberToken);
        var response = await client.PatchAsJsonAsync(
            $"/api/members/{member.Id}/hourly-rates/{rateId}",
            new
            {
                hourlyRate = 15m,
                validFrom = "2026-01-01",
                validTo = (string?)null
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _ = adminToken;
    }

    [Fact]
    public async Task Admin_CorrectUnknownRate_Returns404()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var (member, _) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PatchAsJsonAsync(
            $"/api/members/{member.Id}/hourly-rates/{Guid.NewGuid()}",
            new
            {
                hourlyRate = 15m,
                validFrom = "2026-01-01",
                validTo = (string?)null
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

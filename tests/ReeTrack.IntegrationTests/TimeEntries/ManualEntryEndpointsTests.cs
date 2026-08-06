using System.Net;
using System.Net.Http.Json;
using ReeTrack.Application.Common.Models;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.TimeEntries;

public class ManualEntryEndpointsTests
{
    [Fact]
    public async Task CreateManualEntry_PersistsAndLists()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var response = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Manual design review",
            startedAtUtc,
            endedAtUtc
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.NotNull(body);
        Assert.Equal("Manual", body!.Mode);
        Assert.Equal("Manual design review", body.Description);
        Assert.Equal(3600, body.DurationSeconds);

        var list = await client.GetAsync("/api/time-entries");
        var page = await list.Content.ReadFromJsonAsync<PagedResult<TimeEntryResponse>>();
        Assert.Single(page!.Items);
        Assert.Equal(body.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task CreateManualEntry_EndBeforeStart_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/time-entries", new
        {
            startedAtUtc = DateTime.UtcNow.AddHours(-1),
            endedAtUtc = DateTime.UtcNow.AddHours(-2)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateManualEntry_FutureRange_SavesSuccessfully()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Future work block",
            startedAtUtc = DateTime.UtcNow.AddHours(2),
            endedAtUtc = DateTime.UtcNow.AddHours(3)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.Equal(3600, body!.DurationSeconds);
    }

    [Fact]
    public async Task CreateManualEntry_DurationOver24Hours_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/time-entries", new
        {
            startedAtUtc = DateTime.UtcNow.AddHours(-30),
            endedAtUtc = DateTime.UtcNow.AddHours(-5)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateManualEntry_OverlapIsRejected()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var startedAtUtc = DateTime.UtcNow.AddHours(-3);
        var endedAtUtc = DateTime.UtcNow.AddHours(-2);

        var first = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Existing entry",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var overlapAttempt = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Overlapping entry",
            startedAtUtc = startedAtUtc.AddMinutes(30),
            endedAtUtc = endedAtUtc.AddMinutes(30)
        });
        Assert.Equal(HttpStatusCode.Conflict, overlapAttempt.StatusCode);

        var confirmedStillRejected = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Overlapping entry",
            startedAtUtc = startedAtUtc.AddMinutes(30),
            endedAtUtc = endedAtUtc.AddMinutes(30),
            confirmOverlap = true
        });
        Assert.Equal(HttpStatusCode.Conflict, confirmedStillRejected.StatusCode);
    }

    private sealed class TimeEntryResponse
    {
        public Guid Id { get; set; }
        public string? Description { get; set; }
        public bool IsBillable { get; set; }
        public string Mode { get; set; } = "";
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        public int DurationSeconds { get; set; }
        public bool IsRunning { get; set; }
    }
}

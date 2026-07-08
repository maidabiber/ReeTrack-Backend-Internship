using System.Net;
using System.Net.Http.Json;
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

        var response = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Manual design review",
            startedAtUtc,
            endedAtUtc
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateManualEntryResponse>();
        Assert.NotNull(body);
        Assert.Equal("Manual", body!.Entry.Mode);
        Assert.Equal("Manual design review", body.Entry.Description);
        Assert.Equal(3600, body.Entry.DurationSeconds);
        Assert.Null(body.OverlapWarning);

        var list = await client.GetAsync("/api/time-entries");
        var entries = await list.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(entries!);
        Assert.Equal(body.Entry.Id, entries![0].Id);
    }

    [Fact]
    public async Task CreateManualEntry_EndBeforeStart_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/time-entries/manual", new
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

        var response = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Future work block",
            startedAtUtc = DateTime.UtcNow.AddHours(2),
            endedAtUtc = DateTime.UtcNow.AddHours(3)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateManualEntryResponse>();
        Assert.Equal(3600, body!.Entry.DurationSeconds);
    }

    [Fact]
    public async Task CreateManualEntry_DurationOver24Hours_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            startedAtUtc = DateTime.UtcNow.AddHours(-30),
            endedAtUtc = DateTime.UtcNow.AddHours(-5)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateManualEntry_OverlapRequiresConfirmation()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var startedAtUtc = DateTime.UtcNow.AddHours(-3);
        var endedAtUtc = DateTime.UtcNow.AddHours(-2);

        var first = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Existing entry",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var overlapAttempt = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Overlapping entry",
            startedAtUtc = startedAtUtc.AddMinutes(30),
            endedAtUtc = endedAtUtc.AddMinutes(30),
            confirmOverlap = false
        });
        Assert.Equal(HttpStatusCode.Conflict, overlapAttempt.StatusCode);

        var confirmed = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Overlapping entry",
            startedAtUtc = startedAtUtc.AddMinutes(30),
            endedAtUtc = endedAtUtc.AddMinutes(30),
            confirmOverlap = true
        });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var body = await confirmed.Content.ReadFromJsonAsync<CreateManualEntryResponse>();
        Assert.NotNull(body?.OverlapWarning);
    }

    private sealed class CreateManualEntryResponse
    {
        public TimeEntryResponse Entry { get; set; } = null!;
        public string? OverlapWarning { get; set; }
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

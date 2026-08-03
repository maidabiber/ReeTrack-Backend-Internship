using System.Net;
using System.Net.Http.Json;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.TimeEntries;

public class TimerEndpointsTests
{
    [Fact]
    public async Task StartStopAndListTimerEntry()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var start = await client.PostAsJsonAsync("/api/time-entries/timer/start", new
        {
            description = "Integration test task"
        });

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var running = await start.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.NotNull(running);
        Assert.True(running.IsRunning);
        Assert.NotNull(running.StartedAtUtc);
        Assert.Null(running.EndedAtUtc);
        Assert.Equal("Integration test task", running.Description);

        var active = await client.GetAsync("/api/time-entries/timer/active");
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        var activeBody = await active.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.Equal(running.Id, activeBody!.Id);

        await Task.Delay(1100);

        var stop = await client.PostAsJsonAsync("/api/time-entries/timer/stop", new
        {
            description = "Integration test task (done)"
        });

        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        var stopBody = await stop.Content.ReadFromJsonAsync<StopTimerResponse>();
        Assert.NotNull(stopBody);
        Assert.False(stopBody.HasOverlap);
        var completed = stopBody.Entry;
        Assert.NotNull(completed);
        Assert.False(completed.IsRunning);
        Assert.NotNull(completed.EndedAtUtc);
        Assert.True(completed.DurationSeconds >= 1);
        Assert.Equal("Integration test task (done)", completed.Description);

        var noActive = await client.GetAsync("/api/time-entries/timer/active");
        Assert.Equal(HttpStatusCode.NoContent, noActive.StatusCode);

        var list = await client.GetAsync("/api/time-entries");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var entries = await list.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(entries!);
        Assert.Equal(completed.Id, entries![0].Id);
    }

    [Fact]
    public async Task StartTimer_WhenAlreadyRunning_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var first = await client.PostAsJsonAsync("/api/time-entries/timer/start", new { });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/time-entries/timer/start", new { });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task StopTimer_WhenNotRunning_ReturnsNotFound()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/time-entries/timer/stop", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StopTimer_WhenNotRunning_ReturnsNotFound()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/time-entries/timer/stop", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class StopTimerResponse
    {
        public TimeEntryResponse Entry { get; set; } = null!;
        public bool HasOverlap { get; set; }
        public string? OverlapMessage { get; set; }
        public DateTime? SuggestedClipEndedAtUtc { get; set; }
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

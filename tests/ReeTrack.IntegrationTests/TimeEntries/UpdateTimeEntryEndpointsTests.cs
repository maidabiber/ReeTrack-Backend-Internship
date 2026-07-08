using System.Net;
using System.Net.Http.Json;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.TimeEntries;

public class UpdateTimeEntryEndpointsTests
{
    [Fact]
    public async Task UpdateTimeEntry_RecomputesDuration()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var startedAtUtc = DateTime.UtcNow.AddHours(-3);
        var endedAtUtc = DateTime.UtcNow.AddHours(-2);

        var created = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Original",
            startedAtUtc,
            endedAtUtc
        });
        var createdBody = await created.Content.ReadFromJsonAsync<CreateManualEntryResponse>();
        var entryId = createdBody!.Entry.Id;

        var newStart = DateTime.UtcNow.AddHours(-5);
        var newEnd = DateTime.UtcNow.AddHours(-3);

        var response = await client.PutAsJsonAsync($"/api/time-entries/{entryId}", new
        {
            description = "Updated",
            startedAtUtc = newStart,
            endedAtUtc = newEnd,
            isBillable = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UpdateTimeEntryResponse>();
        Assert.Equal("Updated", body!.Entry.Description);
        Assert.False(body.Entry.IsBillable);
        Assert.Equal(7200, body.Entry.DurationSeconds);
    }

    [Fact]
    public async Task UpdateTimeEntry_NotFound_Returns404()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync($"/api/time-entries/{Guid.NewGuid()}", new
        {
            startedAtUtc = DateTime.UtcNow.AddHours(-2),
            endedAtUtc = DateTime.UtcNow.AddHours(-1)
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTimeEntry_OverlapRequiresConfirmation()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var startedAtUtc = DateTime.UtcNow.AddHours(-4);
        var endedAtUtc = DateTime.UtcNow.AddHours(-3);

        await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Existing",
            startedAtUtc,
            endedAtUtc
        });

        var second = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Movable",
            startedAtUtc = DateTime.UtcNow.AddHours(-6),
            endedAtUtc = DateTime.UtcNow.AddHours(-5)
        });
        var secondBody = await second.Content.ReadFromJsonAsync<CreateManualEntryResponse>();

        var conflict = await client.PutAsJsonAsync($"/api/time-entries/{secondBody!.Entry.Id}", new
        {
            description = "Movable",
            startedAtUtc = startedAtUtc.AddMinutes(30),
            endedAtUtc = endedAtUtc.AddMinutes(30),
            confirmOverlap = false
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var confirmed = await client.PutAsJsonAsync($"/api/time-entries/{secondBody.Entry.Id}", new
        {
            description = "Movable",
            startedAtUtc = startedAtUtc.AddMinutes(30),
            endedAtUtc = endedAtUtc.AddMinutes(30),
            confirmOverlap = true
        });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    private sealed class CreateManualEntryResponse
    {
        public TimeEntryResponse Entry { get; set; } = null!;
    }

    private sealed class UpdateTimeEntryResponse
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

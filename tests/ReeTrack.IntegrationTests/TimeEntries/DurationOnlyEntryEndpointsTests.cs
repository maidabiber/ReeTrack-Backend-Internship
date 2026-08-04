using System.Net;
using System.Net.Http.Json;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.TimeEntries;

public class DurationOnlyEntryEndpointsTests
{
    [Fact]
    public async Task CreateDurationOnlyEntry_PersistsAndLists()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var entryDateUtc = DateTime.UtcNow.Date.AddHours(12);

        var response = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Research without timestamps",
            entryDateUtc,
            durationSeconds = 5400
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.NotNull(body);
        Assert.Equal("DurationOnly", body!.Mode);
        Assert.Equal("Research without timestamps", body.Description);
        Assert.Equal(5400, body.DurationSeconds);
        Assert.NotNull(body.StartedAtUtc);
        Assert.Null(body.EndedAtUtc);

        var list = await client.GetAsync("/api/time-entries");
        var entries = await list.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(entries!);
        Assert.Equal(body.Id, entries![0].Id);
    }

    [Fact]
    public async Task CreateDurationOnlyEntry_InvalidDuration_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var entryDateUtc = DateTime.UtcNow.Date.AddHours(12);

        var zeroDuration = await client.PostAsJsonAsync("/api/time-entries", new
        {
            entryDateUtc,
            durationSeconds = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, zeroDuration.StatusCode);

        var overLimit = await client.PostAsJsonAsync("/api/time-entries", new
        {
            entryDateUtc,
            durationSeconds = 24 * 60 * 60 + 1
        });
        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
    }

    [Fact]
    public async Task UpdateDurationOnlyEntry_UpdatesFields()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var entryDateUtc = DateTime.UtcNow.Date.AddDays(-1).AddHours(12);

        var create = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Initial duration entry",
            entryDateUtc,
            durationSeconds = 1800,
            isBillable = true
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.NotNull(created);

        var updatedDateUtc = DateTime.UtcNow.Date.AddHours(12);
        var update = await client.PutAsJsonAsync($"/api/time-entries/{created!.Id}", new
        {
            description = "Updated duration entry",
            entryDateUtc = updatedDateUtc,
            durationSeconds = 3600,
            isBillable = false
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Updated duration entry", updated!.Description);
        Assert.Equal(3600, updated.DurationSeconds);
        Assert.False(updated.IsBillable);
        Assert.NotNull(updated.StartedAtUtc);
        Assert.Null(updated.EndedAtUtc);
    }

    [Fact]
    public async Task UpdateDurationOnlyEntry_OnManualEntry_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var manual = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Manual entry",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        var manualBody = await manual.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.NotNull(manualBody);

        var update = await client.PutAsJsonAsync($"/api/time-entries/{manualBody!.Id}", new
        {
            entryDateUtc = DateTime.UtcNow.Date.AddHours(12),
            durationSeconds = 1200
        });
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Fact]
    public async Task CreateDurationOnlyEntry_DoesNotConflictWithManualOnSameDay()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var day = DateTime.UtcNow.Date;
        var entryDateUtc = day.AddHours(12);

        var manual = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Manual midday block",
            startedAtUtc = entryDateUtc,
            endedAtUtc = entryDateUtc.AddHours(2)
        });
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);

        var durationOnly = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Duration without clock",
            entryDateUtc,
            durationSeconds = 5400
        });
        Assert.Equal(HttpStatusCode.OK, durationOnly.StatusCode);
        var body = await durationOnly.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.Equal("DurationOnly", body!.Mode);
    }

    [Fact]
    public async Task CreateManualEntry_DoesNotConflictWithDurationOnlyOnSameDay()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var day = DateTime.UtcNow.Date;
        var entryDateUtc = day.AddHours(12);

        var durationOnly = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Duration without clock",
            entryDateUtc,
            durationSeconds = 5400
        });
        Assert.Equal(HttpStatusCode.OK, durationOnly.StatusCode);

        var manual = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Manual midday block",
            startedAtUtc = entryDateUtc,
            endedAtUtc = entryDateUtc.AddHours(2)
        });
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        var body = await manual.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.Equal("Manual", body!.Mode);
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

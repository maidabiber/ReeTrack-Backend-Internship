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

        var response = await client.PostAsJsonAsync("/api/time-entries/duration", new
        {
            description = "Research without timestamps",
            entryDateUtc,
            durationSeconds = 5400
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateDurationOnlyEntryResponse>();
        Assert.NotNull(body);
        Assert.Equal("DurationOnly", body!.Entry.Mode);
        Assert.Equal("Research without timestamps", body.Entry.Description);
        Assert.Equal(5400, body.Entry.DurationSeconds);
        Assert.NotNull(body.Entry.StartedAtUtc);
        Assert.Null(body.Entry.EndedAtUtc);

        var list = await client.GetAsync("/api/time-entries");
        var entries = await list.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(entries!);
        Assert.Equal(body.Entry.Id, entries![0].Id);
    }

    [Fact]
    public async Task CreateDurationOnlyEntry_InvalidDuration_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var entryDateUtc = DateTime.UtcNow.Date.AddHours(12);

        var zeroDuration = await client.PostAsJsonAsync("/api/time-entries/duration", new
        {
            entryDateUtc,
            durationSeconds = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, zeroDuration.StatusCode);

        var overLimit = await client.PostAsJsonAsync("/api/time-entries/duration", new
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

        var create = await client.PostAsJsonAsync("/api/time-entries/duration", new
        {
            description = "Initial duration entry",
            entryDateUtc,
            durationSeconds = 1800,
            isBillable = true
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreateDurationOnlyEntryResponse>();
        Assert.NotNull(created);

        var updatedDateUtc = DateTime.UtcNow.Date.AddHours(12);
        var update = await client.PutAsJsonAsync($"/api/time-entries/{created!.Entry.Id}/duration", new
        {
            description = "Updated duration entry",
            entryDateUtc = updatedDateUtc,
            durationSeconds = 3600,
            isBillable = false
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<UpdateDurationOnlyEntryResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Updated duration entry", updated!.Entry.Description);
        Assert.Equal(3600, updated.Entry.DurationSeconds);
        Assert.False(updated.Entry.IsBillable);
        Assert.NotNull(updated.Entry.StartedAtUtc);
        Assert.Null(updated.Entry.EndedAtUtc);
    }

    [Fact]
    public async Task UpdateDurationOnlyEntry_OnManualEntry_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var manual = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Manual entry",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        var manualBody = await manual.Content.ReadFromJsonAsync<CreateDurationOnlyEntryResponse>();
        Assert.NotNull(manualBody);

        var update = await client.PutAsJsonAsync($"/api/time-entries/{manualBody!.Entry.Id}/duration", new
        {
            entryDateUtc = DateTime.UtcNow.Date.AddHours(12),
            durationSeconds = 1200
        });
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    private sealed class CreateDurationOnlyEntryResponse
    {
        public TimeEntryResponse Entry { get; set; } = null!;
    }

    private sealed class UpdateDurationOnlyEntryResponse
    {
        public TimeEntryResponse Entry { get; set; } = null!;
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

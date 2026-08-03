using System.Net;
using System.Net.Http.Json;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.TimeEntries;

public class SharedTimeEntryEndpointsTests
{
    [Fact]
    public async Task CreateSharedManualEntry_CreatesPendingEntryForAssignee()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();

        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var response = await adminClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserId = member.Id,
            description = "Pair programming",
            startedAtUtc,
            endedAtUtc
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        Assert.NotNull(body);
        var owned = Assert.Single(body!.Entries);
        Assert.Equal("Confirmed", owned.Status);
        Assert.Equal(admin.Id, owned.AssigneeUserId);
        Assert.Null(owned.SubmittedByUserId);
        Assert.NotNull(owned.ShareGroupId);
        Assert.Equal(2, owned.Participants.Count);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(adminEntries!);
        Assert.Equal("Confirmed", adminEntries![0].Status);
        Assert.Equal(admin.Id, adminEntries[0].AssigneeUserId);
        Assert.Equal(owned.ShareGroupId, adminEntries[0].ShareGroupId);

        var memberList = await memberClient.GetAsync("/api/time-entries");
        var memberEntries = await memberList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(memberEntries!);
        Assert.Equal("Pending", memberEntries![0].Status);
        Assert.Equal(owned.ShareGroupId, memberEntries[0].ShareGroupId);

        var pendingList = await memberClient.GetAsync("/api/time-entries/pending");
        var pendingEntries = await pendingList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(pendingEntries!);
        Assert.Equal(memberEntries[0].Id, pendingEntries![0].Id);
        Assert.Equal(admin.Id, pendingEntries[0].SubmittedByUserId);
        Assert.Equal("Test Admin", pendingEntries[0].SubmittedByDisplayName);
    }

    [Fact]
    public async Task StopSharedTimer_CreatesPendingEntryForAssignee()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();

        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var start = await adminClient.PostAsJsonAsync("/api/time-entries/timer/start", new
        {
            description = "Pair programming session"
        });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        await Task.Delay(1100);

        var stop = await adminClient.PostAsJsonAsync("/api/time-entries/timer/stop", new
        {
            description = "Pair programming session",
            assigneeUserIds = new[] { member.Id }
        });

        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        var body = await stop.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        Assert.NotNull(body);
        var owned = Assert.Single(body!.Entries);
        Assert.Equal("Confirmed", owned.Status);
        Assert.Equal("Timer", owned.Mode);
        Assert.Equal(admin.Id, owned.AssigneeUserId);
        Assert.True(owned.DurationSeconds >= 1);

        var noActive = await adminClient.GetAsync("/api/time-entries/timer/active");
        Assert.Equal(HttpStatusCode.NoContent, noActive.StatusCode);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(adminEntries!);
        Assert.Equal("Confirmed", adminEntries![0].Status);
        Assert.Equal(admin.Id, adminEntries[0].AssigneeUserId);

        var memberList = await memberClient.GetAsync("/api/time-entries");
        var memberEntries = await memberList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(memberEntries!);
        Assert.Equal("Pending", memberEntries![0].Status);
        Assert.Equal("Timer", memberEntries[0].Mode);
        Assert.Equal(owned.ShareGroupId, memberEntries[0].ShareGroupId);
    }

    [Fact]
    public async Task ShareExistingEntry_OnSoloConfirmedEntry_CreatesPendingForAssignee()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();

        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var create = await adminClient.PostAsJsonAsync("/api/time-entries", new
        {
            description = "Solo work",
            startedAtUtc,
            endedAtUtc
        });
        var created = await create.Content.ReadFromJsonAsync<TimeEntryResponse>();

        var share = await adminClient.PostAsJsonAsync($"/api/time-entries/{created!.Id}/share", new
        {
            assigneeUserIds = new[] { member.Id }
        });

        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
        var body = await share.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        Assert.NotNull(body);
        var owned = Assert.Single(body!.Entries);
        Assert.Equal("Confirmed", owned.Status);
        Assert.Equal(admin.Id, owned.AssigneeUserId);
        Assert.Equal(created.Id, owned.Id);
        Assert.NotNull(owned.ShareGroupId);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(adminEntries!);
        Assert.Equal("Confirmed", adminEntries![0].Status);
        Assert.Equal(admin.Id, adminEntries[0].AssigneeUserId);
        Assert.Equal(owned.ShareGroupId, adminEntries[0].ShareGroupId);

        var memberList = await memberClient.GetAsync("/api/time-entries");
        var memberEntries = await memberList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(memberEntries!);
        Assert.Equal("Pending", memberEntries![0].Status);
        Assert.Equal(owned.ShareGroupId, memberEntries[0].ShareGroupId);
    }

    [Fact]
    public async Task ShareExistingEntry_OnSharedEntry_AddsAnotherAssignee()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();
        var (other, otherToken) = await factory.SeedMemberAsync(
            email: "other.member@reetrack.test",
            displayName: "Other Member");

        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);
        var otherClient = factory.CreateAuthenticatedClient(otherToken);

        var startedAtUtc = DateTime.UtcNow.AddHours(-3);
        var endedAtUtc = DateTime.UtcNow.AddHours(-2);

        var create = await adminClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserId = member.Id,
            description = "Shared work",
            startedAtUtc,
            endedAtUtc
        });
        var created = await create.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        var owned = Assert.Single(created!.Entries);
        Assert.NotNull(owned.ShareGroupId);

        var memberPending = await memberClient.GetAsync("/api/time-entries/pending");
        var memberPendingEntries = await memberPending.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        var pendingId = Assert.Single(memberPendingEntries!).Id;

        var share = await adminClient.PostAsJsonAsync($"/api/time-entries/{pendingId}/share", new
        {
            assigneeUserIds = new[] { other.Id }
        });

        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
        var body = await share.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        Assert.NotNull(body);
        Assert.Empty(body!.Entries);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(adminEntries!);
        Assert.Equal(owned.ShareGroupId, adminEntries![0].ShareGroupId);

        var otherList = await otherClient.GetAsync("/api/time-entries/pending");
        var otherPending = await otherList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(otherPending!);
        Assert.Equal(owned.ShareGroupId, otherPending![0].ShareGroupId);
        Assert.Equal(other.Id, otherPending[0].AssigneeUserId);
    }

    [Fact]
    public async Task UpdatePendingEntry_AllowsAssigneeToEditBeforeApproval()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();

        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var startedAtUtc = DateTime.UtcNow.AddHours(-3);
        var endedAtUtc = DateTime.UtcNow.AddHours(-2);

        var create = await adminClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserId = member.Id,
            description = "Shared work",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var pendingList = await memberClient.GetAsync("/api/time-entries/pending");
        var pendingEntries = await pendingList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        var pendingId = Assert.Single(pendingEntries!).Id;

        var newStart = startedAtUtc.AddMinutes(-30);
        var newEnd = endedAtUtc.AddMinutes(30);

        var update = await memberClient.PutAsJsonAsync($"/api/time-entries/pending/{pendingId}", new
        {
            description = "Adjusted shared work",
            startedAtUtc = newStart,
            endedAtUtc = newEnd,
            isBillable = false
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.Equal("Adjusted shared work", updated!.Description);
        Assert.Equal(7200, updated.DurationSeconds);
        Assert.False(updated.IsBillable);
    }

    [Fact]
    public async Task ApprovePendingEntry_MovesEntryToConfirmedList()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();

        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var create = await adminClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserId = member.Id,
            description = "Shared work",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var pendingList = await memberClient.GetAsync("/api/time-entries/pending");
        var pendingEntries = await pendingList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        var pendingId = Assert.Single(pendingEntries!).Id;

        var approve = await memberClient.PostAsync($"/api/time-entries/pending/{pendingId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approved = await approve.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.Equal("Confirmed", approved!.Status);

        var pending = await memberClient.GetAsync("/api/time-entries/pending");
        var remainingPending = await pending.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Empty(remainingPending!);

        var list = await memberClient.GetAsync("/api/time-entries");
        var entries = await list.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(entries!);
        Assert.Equal(pendingId, entries![0].Id);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(adminEntries!);
        Assert.Equal("Confirmed", adminEntries![0].Status);
        Assert.Equal(admin.Id, adminEntries[0].AssigneeUserId);
        Assert.Null(adminEntries[0].SubmittedByUserId);
    }

    [Fact]
    public async Task ListTeammates_ExcludesCurrentUser()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        await factory.SeedMemberAsync();

        var client = factory.CreateAuthenticatedClient(adminToken);
        var response = await client.GetAsync("/api/teammates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teammates = await response.Content.ReadFromJsonAsync<List<TeammateResponse>>();
        Assert.Single(teammates!);
        Assert.Equal("Test Member", teammates![0].DisplayName);
    }
}

internal sealed class TeammateResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

internal sealed class CreateSharedManualEntryResponse
{
    public List<TimeEntryResponse> Entries { get; set; } = [];
}

internal sealed class TimeEntryResponse
{
    public Guid Id { get; set; }
    public string? Description { get; set; }
    public bool IsBillable { get; set; }
    public string Mode { get; set; } = "";
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int DurationSeconds { get; set; }
    public bool IsRunning { get; set; }
    public string Status { get; set; } = "";
    public Guid? SubmittedByUserId { get; set; }
    public string? SubmittedByDisplayName { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public string? AssigneeDisplayName { get; set; }
    public Guid? ShareGroupId { get; set; }
    public List<TimeEntryParticipantResponse> Participants { get; set; } = [];
}

internal sealed class TimeEntryParticipantResponse
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

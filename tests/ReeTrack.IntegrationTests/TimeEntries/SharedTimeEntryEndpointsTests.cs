using System.Net;
using System.Net.Http.Json;
using ReeTrack.Application.Common.Models;
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

        var adminEntries = await ListTimeEntriesAsync(adminClient);
        Assert.Single(adminEntries);
        Assert.Equal("Confirmed", adminEntries[0].Status);
        Assert.Equal(admin.Id, adminEntries[0].AssigneeUserId);
        Assert.Equal(owned.ShareGroupId, adminEntries[0].ShareGroupId);

        var memberEntries = await ListTimeEntriesAsync(memberClient);
        Assert.Single(memberEntries);
        Assert.Equal("Pending", memberEntries[0].Status);
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
            description = "Pair programming session"
        });

        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        var stopBody = await stop.Content.ReadFromJsonAsync<StopTimerResponse>();
        Assert.NotNull(stopBody);
        Assert.False(stopBody.HasOverlap);
        var stopped = stopBody.Entry;
        Assert.Equal("Confirmed", stopped.Status);
        Assert.Equal("Timer", stopped.Mode);
        Assert.True(stopped.DurationSeconds >= 1);

        var share = await adminClient.PostAsJsonAsync($"/api/time-entries/{stopped.Id}/share", new
        {
            assigneeUserIds = new[] { member.Id }
        });
        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
        var body = await share.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        Assert.NotNull(body);
        var owned = Assert.Single(body!.Entries);
        Assert.Equal("Confirmed", owned.Status);
        Assert.Equal("Timer", owned.Mode);
        Assert.Equal(admin.Id, owned.AssigneeUserId);
        Assert.True(owned.DurationSeconds >= 1);

        var noActive = await adminClient.GetAsync("/api/time-entries/timer/active");
        Assert.Equal(HttpStatusCode.NoContent, noActive.StatusCode);

        var adminEntries = await ListTimeEntriesAsync(adminClient);
        Assert.Single(adminEntries);
        Assert.Equal("Confirmed", adminEntries[0].Status);
        Assert.Equal(admin.Id, adminEntries[0].AssigneeUserId);

        var memberEntries = await ListTimeEntriesAsync(memberClient);
        Assert.Single(memberEntries);
        Assert.Equal("Pending", memberEntries[0].Status);
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

        var adminEntries = await ListTimeEntriesAsync(adminClient);
        Assert.Single(adminEntries);
        Assert.Equal("Confirmed", adminEntries[0].Status);
        Assert.Equal(admin.Id, adminEntries[0].AssigneeUserId);
        Assert.Equal(owned.ShareGroupId, adminEntries[0].ShareGroupId);

        var memberEntries = await ListTimeEntriesAsync(memberClient);
        Assert.Single(memberEntries);
        Assert.Equal("Pending", memberEntries[0].Status);
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

        var adminEntries = await ListTimeEntriesAsync(adminClient);
        Assert.Single(adminEntries);
        Assert.Equal(owned.ShareGroupId, adminEntries[0].ShareGroupId);

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

        var entries = await ListTimeEntriesAsync(memberClient);
        Assert.Single(entries);
        Assert.Equal(pendingId, entries[0].Id);

        var adminEntries = await ListTimeEntriesAsync(adminClient);
        Assert.Single(adminEntries);
        Assert.Equal("Confirmed", adminEntries[0].Status);
        Assert.Equal(admin.Id, adminEntries[0].AssigneeUserId);
        Assert.Null(adminEntries[0].SubmittedByUserId);
    }

    [Fact]
    public async Task ApprovePendingEntry_WithEdits_AppliesChangesAndConfirms()
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
            endedAtUtc,
            isBillable = true
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var pendingList = await memberClient.GetAsync("/api/time-entries/pending");
        var pendingEntries = await pendingList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        var pendingId = Assert.Single(pendingEntries!).Id;

        var newStart = startedAtUtc.AddMinutes(-30);
        var newEnd = endedAtUtc.AddMinutes(30);

        var approve = await memberClient.PostAsJsonAsync(
            $"/api/time-entries/pending/{pendingId}/approve",
            new
            {
                description = "Approved with edits",
                startedAtUtc = newStart,
                endedAtUtc = newEnd,
                isBillable = false
            });

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approved = await approve.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.Equal("Confirmed", approved!.Status);
        Assert.Equal("Approved with edits", approved.Description);
        Assert.False(approved.IsBillable);
        Assert.Equal(7200, approved.DurationSeconds);

        var list = await memberClient.GetAsync("/api/time-entries");
        var entries = await list.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        var confirmed = Assert.Single(entries!);
        Assert.Equal(pendingId, confirmed.Id);
        Assert.Equal("Approved with edits", confirmed.Description);
        Assert.False(confirmed.IsBillable);
        Assert.Equal(7200, confirmed.DurationSeconds);
    }

    

    [Fact]
    public async Task ApprovePendingEntry_WhenOverlapsAnotherPending_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();
        var (other, otherToken) = await factory.SeedMemberAsync(
            email: "other.sharer@reetrack.test",
            displayName: "Other Sharer");

        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var otherClient = factory.CreateAuthenticatedClient(otherToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var first = await adminClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserId = member.Id,
            description = "Pending A",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await otherClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserId = member.Id,
            description = "Pending B",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var pendingList = await memberClient.GetAsync("/api/time-entries/pending");
        var pendingEntries = await pendingList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Equal(2, pendingEntries!.Count);

        var firstPendingId = pendingEntries.Single(e => e.Description == "Pending A").Id;

        var approveFirst = await memberClient.PostAsync(
            $"/api/time-entries/pending/{firstPendingId}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, approveFirst.StatusCode);
    }

        

    [Fact]
    public async Task ShareExistingEntry_WhenAssigneeHasOverlappingPending_Succeeds()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();
        var (other, otherToken) = await factory.SeedMemberAsync(
            email: "other.member@reetrack.test",
            displayName: "Other Member");

        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var otherClient = factory.CreateAuthenticatedClient(otherToken);

        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var firstShare = await adminClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserId = member.Id,
            description = "First shared slot",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, firstShare.StatusCode);

        var secondShare = await otherClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserId = member.Id,
            description = "Second overlapping share",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, secondShare.StatusCode);

        var memberClient = factory.CreateAuthenticatedClient(memberToken);
        var pendingList = await memberClient.GetAsync("/api/time-entries/pending");
        var pendingEntries = await pendingList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Equal(2, pendingEntries!.Count);
    }

    

    [Fact]
    public async Task RejectPendingEntry_SoftDeletesCopy_LeavesSourceAndOtherAssignees()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();
        var (other, otherToken) = await factory.SeedMemberAsync(
            email: "other.assignee@reetrack.test",
            displayName: "Other Assignee");

        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);
        var otherClient = factory.CreateAuthenticatedClient(otherToken);

        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var create = await adminClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserIds = new[] { member.Id, other.Id },
            description = "Shared with two",
            startedAtUtc,
            endedAtUtc
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var body = await create.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Entries);
        var sourceId = body.Entries[0].Id;

        var memberPending = await memberClient.GetAsync("/api/time-entries/pending");
        var memberPendingEntries = await memberPending.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        var memberPendingId = Assert.Single(memberPendingEntries!).Id;

        var otherPendingBefore = await otherClient.GetAsync("/api/time-entries/pending");
        var otherPendingEntriesBefore =
            await otherPendingBefore.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        var otherPendingId = Assert.Single(otherPendingEntriesBefore!).Id;

        var reject = await memberClient.PostAsync(
            $"/api/time-entries/pending/{memberPendingId}/reject", null);
        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);

        var memberPendingAfter = await memberClient.GetAsync("/api/time-entries/pending");
        var remainingMemberPending =
            await memberPendingAfter.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Empty(remainingMemberPending!);

        var memberEntries = await ListTimeEntriesAsync(memberClient);
        Assert.Empty(memberEntries);

        var adminEntries = await ListTimeEntriesAsync(adminClient);
        Assert.Single(adminEntries);
        Assert.Equal(sourceId, adminEntries[0].Id);
        Assert.Equal("Confirmed", adminEntries[0].Status);

        var otherPendingAfter = await otherClient.GetAsync("/api/time-entries/pending");
        var remainingOtherPending =
            await otherPendingAfter.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(remainingOtherPending!);
        Assert.Equal(otherPendingId, remainingOtherPending![0].Id);
    }

    private static async Task<IReadOnlyList<TimeEntryResponse>> ListTimeEntriesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/time-entries");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<TimeEntryResponse>>();
        Assert.NotNull(page);
        return page!.Items;
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

internal sealed class StopTimerResponse
{
    public TimeEntryResponse Entry { get; set; } = null!;
    public bool HasOverlap { get; set; }
    public string? OverlapMessage { get; set; }
    public DateTime? SuggestedClipEndedAtUtc { get; set; }
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

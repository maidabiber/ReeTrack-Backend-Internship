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

        var response = await adminClient.PostAsJsonAsync("/api/time-entries/shared/manual", new
        {
            assigneeUserId = member.Id,
            description = "Pair programming",
            startedAtUtc,
            endedAtUtc
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Entries.Count);

        var owned = Assert.Single(body.Entries, e => e.Status == "Confirmed");
        var pending = Assert.Single(body.Entries, e => e.Status == "Pending");
        Assert.Equal(admin.Id, owned.AssigneeUserId);
        Assert.Null(owned.SubmittedByUserId);
        Assert.Equal(member.Id, pending.AssigneeUserId);
        Assert.Equal(admin.Id, pending.SubmittedByUserId);
        Assert.Equal(owned.ShareGroupId, pending.ShareGroupId);
        Assert.NotNull(owned.ShareGroupId);
        Assert.Equal(2, pending.Participants.Count);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Equal(2, adminEntries!.Count);
        Assert.Contains(adminEntries, e => e.Status == "Confirmed" && e.AssigneeUserId == admin.Id);
        Assert.Contains(adminEntries, e => e.Status == "Pending" && e.AssigneeUserId == member.Id);

        var memberList = await memberClient.GetAsync("/api/time-entries");
        var memberEntries = await memberList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(memberEntries!);
        Assert.Equal("Pending", memberEntries![0].Status);
        Assert.Equal("Test Admin", memberEntries[0].SubmittedByDisplayName);

        var pendingList = await memberClient.GetAsync("/api/time-entries/pending");
        var pendingEntries = await pendingList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(pendingEntries!);
        Assert.Equal(pending.Id, pendingEntries![0].Id);
        Assert.Equal("Test Admin", pendingEntries[0].SubmittedByDisplayName);

        await factory.EmailSender.WaitForMentionEmailAsync(member.Email);
        Assert.Contains("/approvals", factory.EmailSender.LastMentionReviewUrl, StringComparison.Ordinal);
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
        Assert.Equal(2, body!.Entries.Count);
        Assert.Contains(body.Entries, e => e.Status == "Confirmed" && e.Mode == "Timer" && e.AssigneeUserId == admin.Id);
        Assert.Contains(body.Entries, e => e.Status == "Pending" && e.Mode == "Timer" && e.AssigneeUserId == member.Id);
        Assert.All(body.Entries, e => Assert.True(e.DurationSeconds >= 1));

        var noActive = await adminClient.GetAsync("/api/time-entries/timer/active");
        Assert.Equal(HttpStatusCode.NoContent, noActive.StatusCode);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Equal(2, adminEntries!.Count);
        Assert.Contains(adminEntries, e => e.Status == "Confirmed" && e.AssigneeUserId == admin.Id);
        Assert.Contains(adminEntries, e => e.Status == "Pending" && e.AssigneeUserId == member.Id);

        var memberList = await memberClient.GetAsync("/api/time-entries");
        var memberEntries = await memberList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(memberEntries!);
        Assert.Equal("Pending", memberEntries![0].Status);

        await factory.EmailSender.WaitForMentionEmailAsync(member.Email);
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

        var create = await adminClient.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Solo work",
            startedAtUtc,
            endedAtUtc
        });
        var created = await create.Content.ReadFromJsonAsync<CreateManualEntryResponse>();

        var share = await adminClient.PostAsJsonAsync($"/api/time-entries/{created!.Entry.Id}/share", new
        {
            assigneeUserIds = new[] { member.Id }
        });

        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
        var body = await share.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Entries.Count);
        Assert.Contains(body.Entries, e => e.Status == "Confirmed" && e.AssigneeUserId == admin.Id && e.Id == created!.Entry.Id);
        Assert.Contains(body.Entries, e => e.Status == "Pending" && e.AssigneeUserId == member.Id);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Equal(2, adminEntries!.Count);
        Assert.Contains(adminEntries, e => e.Status == "Confirmed" && e.AssigneeUserId == admin.Id);
        Assert.Contains(adminEntries, e => e.Status == "Pending" && e.AssigneeUserId == member.Id);
        Assert.All(adminEntries, e => Assert.Equal(body.Entries[0].ShareGroupId, e.ShareGroupId));

        var memberList = await memberClient.GetAsync("/api/time-entries");
        var memberEntries = await memberList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(memberEntries!);
        Assert.Equal("Pending", memberEntries![0].Status);

        await factory.EmailSender.WaitForMentionEmailAsync(member.Email);
    }

    [Fact]
    public async Task ShareExistingEntry_OnSharedEntry_AddsAnotherAssignee()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();
        var (other, _) = await factory.SeedMemberAsync(email: "other.member@reetrack.test", displayName: "Other Member");

        var adminClient = factory.CreateAuthenticatedClient(adminToken);

        var startedAtUtc = DateTime.UtcNow.AddHours(-3);
        var endedAtUtc = DateTime.UtcNow.AddHours(-2);

        var create = await adminClient.PostAsJsonAsync("/api/time-entries/shared/manual", new
        {
            assigneeUserId = member.Id,
            description = "Shared work",
            startedAtUtc,
            endedAtUtc
        });
        var created = await create.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();

        var share = await adminClient.PostAsJsonAsync($"/api/time-entries/{created!.Entries.Single(e => e.Status == "Pending").Id}/share", new
        {
            assigneeUserIds = new[] { other.Id }
        });

        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
        var body = await share.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();
        Assert.NotNull(body);
        Assert.Single(body!.Entries);
        Assert.Equal(other.Id, body.Entries[0].AssigneeUserId);
        Assert.NotNull(body.Entries[0].ShareGroupId);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Equal(3, adminEntries!.Count);
        Assert.All(adminEntries, entry => Assert.Equal(body.Entries[0].ShareGroupId, entry.ShareGroupId));
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

        var create = await adminClient.PostAsJsonAsync("/api/time-entries/shared/manual", new
        {
            assigneeUserId = member.Id,
            description = "Shared work",
            startedAtUtc,
            endedAtUtc
        });
        var created = await create.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();

        var newStart = startedAtUtc.AddMinutes(-30);
        var newEnd = endedAtUtc.AddMinutes(30);

        var update = await memberClient.PutAsJsonAsync($"/api/time-entries/pending/{created!.Entries.Single(e => e.Status == "Pending").Id}", new
        {
            description = "Adjusted shared work",
            startedAtUtc = newStart,
            endedAtUtc = newEnd,
            isBillable = false
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<UpdateTimeEntryResponse>();
        Assert.Equal("Adjusted shared work", updated!.Entry.Description);
        Assert.Equal(7200, updated.Entry.DurationSeconds);
        Assert.False(updated.Entry.IsBillable);
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

        var create = await adminClient.PostAsJsonAsync("/api/time-entries/shared/manual", new
        {
            assigneeUserId = member.Id,
            description = "Shared work",
            startedAtUtc,
            endedAtUtc
        });
        var created = await create.Content.ReadFromJsonAsync<CreateSharedManualEntryResponse>();

        var pendingId = created!.Entries.Single(e => e.Status == "Pending").Id;
        var approve = await memberClient.PostAsync($"/api/time-entries/pending/{pendingId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approved = await approve.Content.ReadFromJsonAsync<TimeEntryResponse>();
        Assert.Equal("Confirmed", approved!.Status);

        var pending = await memberClient.GetAsync("/api/time-entries/pending");
        var pendingEntries = await pending.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Empty(pendingEntries!);

        var list = await memberClient.GetAsync("/api/time-entries");
        var entries = await list.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Single(entries!);
        Assert.Equal(pendingId, entries![0].Id);

        var adminList = await adminClient.GetAsync("/api/time-entries");
        var adminEntries = await adminList.Content.ReadFromJsonAsync<List<TimeEntryResponse>>();
        Assert.Equal(2, adminEntries!.Count);
        Assert.Contains(adminEntries, e => e.Status == "Confirmed" && e.AssigneeUserId == admin.Id && e.SubmittedByUserId is null);
        Assert.Contains(adminEntries, e => e.Id == pendingId && e.Status == "Confirmed" && e.SubmittedByUserId == admin.Id);
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

internal sealed class CreateManualEntryResponse
{
    public TimeEntryResponse Entry { get; set; } = null!;
}

internal sealed class UpdateTimeEntryResponse
{
    public TimeEntryResponse Entry { get; set; } = null!;
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

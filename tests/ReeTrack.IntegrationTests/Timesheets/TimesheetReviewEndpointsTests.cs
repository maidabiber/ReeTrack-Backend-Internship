using System.Net;
using System.Net.Http.Json;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Timesheets;

public class TimesheetReviewEndpointsTests
{
    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);
    private static DateOnly PreviousWeek => CurrentWeek.AddDays(-7);

    [Fact]
    public async Task ReviewEndpoints_NonAdmin_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/timesheets/review")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/timesheets/review/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync($"/api/timesheets/review/{id}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync($"/api/timesheets/review/{id}/reject", new { })).StatusCode);
    }

    [Fact]
    public async Task Queue_ListsSubmittedOldestFirst_WithTotals()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var first = await SeedSubmittedWeekAsync(factory, "first@reetrack.test", "First Member");
        var second = await SeedSubmittedWeekAsync(factory, "second@reetrack.test", "Second Member", entryCount: 2);

        var response = await adminClient.GetAsync("/api/timesheets/review");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<AdminListItemResponse>>();
        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(2, page.Items.Count);

        // Oldest submission first.
        Assert.Equal(first.TimesheetId, page.Items[0].Id);
        Assert.Equal("First Member", page.Items[0].UserDisplayName);
        Assert.Equal(3600, page.Items[0].TotalSeconds);
        Assert.Equal(1, page.Items[0].EntryCount);

        Assert.Equal(second.TimesheetId, page.Items[1].Id);
        Assert.Equal(7200, page.Items[1].TotalSeconds);
        Assert.Equal(2, page.Items[1].EntryCount);
    }

    [Fact]
    public async Task Queue_Paging_ReturnsRequestedSlice()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var first = await SeedSubmittedWeekAsync(factory, "first@reetrack.test", "First Member");
        var second = await SeedSubmittedWeekAsync(factory, "second@reetrack.test", "Second Member");

        var page2 = await adminClient.GetFromJsonAsync<PagedResponse<AdminListItemResponse>>(
            "/api/timesheets/review?page=2&pageSize=1");

        Assert.Equal(2, page2!.TotalCount);
        Assert.Equal(2, page2.Page);
        var item = Assert.Single(page2.Items);
        Assert.Equal(second.TimesheetId, item.Id);
        _ = first;
    }

    [Fact]
    public async Task Queue_StatusFilter_DefaultExcludesReviewed_AllIncludesEverything()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var submitted = await SeedSubmittedWeekAsync(factory, "member@reetrack.test", "Test Member");
        var approve = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{submitted.TimesheetId}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var defaultQueue = await adminClient.GetFromJsonAsync<PagedResponse<AdminListItemResponse>>(
            "/api/timesheets/review");
        Assert.Equal(0, defaultQueue!.TotalCount);

        var approvedQueue = await adminClient.GetFromJsonAsync<PagedResponse<AdminListItemResponse>>(
            "/api/timesheets/review?status=Approved");
        Assert.Equal(1, approvedQueue!.TotalCount);

        var allQueue = await adminClient.GetFromJsonAsync<PagedResponse<AdminListItemResponse>>(
            "/api/timesheets/review?status=all");
        Assert.Equal(1, allQueue!.TotalCount);

        var invalid = await adminClient.GetAsync("/api/timesheets/review?status=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Detail_ReturnsUserEntriesAndTotals()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var submitted = await SeedSubmittedWeekAsync(factory, "member@reetrack.test", "Test Member", entryCount: 2);

        var response = await adminClient.GetAsync($"/api/timesheets/review/{submitted.TimesheetId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<AdminDetailResponse>();
        Assert.Equal("Test Member", detail!.UserDisplayName);
        Assert.Equal("member@reetrack.test", detail.UserEmail);
        Assert.Equal(2, detail.Entries.Count);
        Assert.Equal(7200, detail.TotalSeconds);
        Assert.Equal("Submitted", detail.Timesheet.Status);

        var missing = await adminClient.GetAsync($"/api/timesheets/review/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Approve_SetsReviewerFields_SendsEmail_AndKeepsWeekLocked()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var submitted = await SeedSubmittedWeekAsync(factory, "member@reetrack.test", "Test Member");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{submitted.TimesheetId}/approve", new { comment = "  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TimesheetResponse>();
        Assert.Equal("Approved", body!.Status);
        Assert.Equal(admin.Id, body.ReviewedByUserId);
        Assert.NotNull(body.ReviewedAtUtc);
        Assert.Null(body.ReviewComment); // blank comment stored as null

        var email = Assert.Single(factory.EmailSender.DecisionEmails);
        Assert.Equal("member@reetrack.test", email.ToEmail);
        Assert.True(email.Approved);
        Assert.Null(email.Comment);
        Assert.Contains($"week={PreviousWeek:yyyy-MM-dd}", email.TimesheetUrl);

        // Approved week stays locked for the member.
        var memberClient = factory.CreateAuthenticatedClient(submitted.MemberToken);
        var edit = await memberClient.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "After approval",
            startedAtUtc = PreviousWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(14),
            endedAtUtc = PreviousWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(15)
        });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
    }

    [Fact]
    public async Task Reject_WithComment_ReopensWeekForResubmission()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var submitted = await SeedSubmittedWeekAsync(factory, "member@reetrack.test", "Test Member");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{submitted.TimesheetId}/reject",
            new { comment = "Missing Tuesday entries." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TimesheetResponse>();
        Assert.Equal("Rejected", body!.Status);
        Assert.Equal("Missing Tuesday entries.", body.ReviewComment);

        var email = Assert.Single(factory.EmailSender.DecisionEmails);
        Assert.False(email.Approved);
        Assert.Equal("Missing Tuesday entries.", email.Comment);

        // Member can edit the week again and resubmit; the sheet re-enters the queue.
        var memberClient = factory.CreateAuthenticatedClient(submitted.MemberToken);
        var monday = PreviousWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var edit = await memberClient.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Tuesday fix",
            startedAtUtc = monday.AddDays(1).AddHours(9),
            endedAtUtc = monday.AddDays(1).AddHours(10)
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var resubmit = await memberClient.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = PreviousWeek });
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);

        var queue = await adminClient.GetFromJsonAsync<PagedResponse<AdminListItemResponse>>("/api/timesheets/review");
        Assert.Equal(1, queue!.TotalCount);
        Assert.Equal(submitted.TimesheetId, queue.Items[0].Id);
    }

    [Fact]
    public async Task Approve_AlreadyApproved_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var submitted = await SeedSubmittedWeekAsync(factory, "member@reetrack.test", "Test Member");

        var approve = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{submitted.TimesheetId}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // Approve only acts on a fresh submission; re-approving an approved sheet 409s.
        var again = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{submitted.TimesheetId}/approve", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task SendBack_AfterApproval_ReopensWeekForResubmission()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var submitted = await SeedSubmittedWeekAsync(factory, "member@reetrack.test", "Test Member");

        var approve = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{submitted.TimesheetId}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // Admin spots an error after approving and sends the sheet back for fixes.
        var sendBack = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{submitted.TimesheetId}/reject",
            new { comment = "Wrong project on Monday." });

        Assert.Equal(HttpStatusCode.OK, sendBack.StatusCode);
        var body = await sendBack.Content.ReadFromJsonAsync<TimesheetResponse>();
        Assert.Equal("Rejected", body!.Status);
        Assert.Equal("Wrong project on Monday.", body.ReviewComment);

        // A second decision email is queued for the send-back.
        Assert.Equal(2, factory.EmailSender.DecisionEmails.Count);
        var latest = factory.EmailSender.DecisionEmails[^1];
        Assert.False(latest.Approved);
        Assert.Equal("Wrong project on Monday.", latest.Comment);

        // The week is editable again and can be resubmitted into the review queue.
        var memberClient = factory.CreateAuthenticatedClient(submitted.MemberToken);
        var monday = PreviousWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var edit = await memberClient.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Monday fix",
            startedAtUtc = monday.AddHours(13),
            endedAtUtc = monday.AddHours(14)
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var resubmit = await memberClient.PostAsJsonAsync(
            "/api/timesheets/my/submit", new { weekStart = PreviousWeek });
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);

        var queue = await adminClient.GetFromJsonAsync<PagedResponse<AdminListItemResponse>>(
            "/api/timesheets/review");
        Assert.Equal(1, queue!.TotalCount);
        Assert.Equal(submitted.TimesheetId, queue.Items[0].Id);
    }

    [Fact]
    public async Task Approve_AfterSendBack_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var submitted = await SeedSubmittedWeekAsync(factory, "member@reetrack.test", "Test Member");

        var reject = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{submitted.TimesheetId}/reject", new { comment = "Fix it" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        // A sent-back sheet must be resubmitted before it can be approved.
        var approve = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{submitted.TimesheetId}/approve", new { });
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    [Fact]
    public async Task Admin_CanApproveOwnTimesheet()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var monday = PreviousWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await CreateManualEntryAsync(adminClient, monday.AddHours(9));
        var submit = await adminClient.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = PreviousWeek });
        var own = await submit.Content.ReadFromJsonAsync<TimesheetResponse>();

        var approve = await adminClient.PostAsJsonAsync($"/api/timesheets/review/{own!.Id}/approve", new { });

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var body = await approve.Content.ReadFromJsonAsync<TimesheetResponse>();
        Assert.Equal("Approved", body!.Status);
    }

    [Fact]
    public async Task Admin_CanSendBackOwnApprovedTimesheet()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var monday = PreviousWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await CreateManualEntryAsync(adminClient, monday.AddHours(9));
        var submit = await adminClient.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = PreviousWeek });
        var own = await submit.Content.ReadFromJsonAsync<TimesheetResponse>();

        var approve = await adminClient.PostAsJsonAsync($"/api/timesheets/review/{own!.Id}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // Reviewing one's own timesheet is the owner==reviewer path: the same User is
        // tracked as the sheet owner and loaded again as the reviewer. Sending the
        // approved sheet back must reuse that single instance, not attach a duplicate.
        var sendBack = await adminClient.PostAsJsonAsync(
            $"/api/timesheets/review/{own.Id}/reject", new { comment = "Fix Monday's hours." });

        Assert.Equal(HttpStatusCode.OK, sendBack.StatusCode);
        var body = await sendBack.Content.ReadFromJsonAsync<TimesheetResponse>();
        Assert.Equal("Rejected", body!.Status);
        Assert.Equal("Fix Monday's hours.", body.ReviewComment);
        Assert.Equal("Test Admin", body.ReviewedByDisplayName);
    }

    private sealed record SubmittedWeek(Guid TimesheetId, string MemberToken);

    /// <summary>Seeds a member with hour-long entries in the previous week and submits it.</summary>
    private static async Task<SubmittedWeek> SeedSubmittedWeekAsync(
        ReeTrackWebApplicationFactory factory,
        string email,
        string displayName,
        int entryCount = 1)
    {
        var (_, token) = await factory.SeedMemberAsync(email, displayName);
        var client = factory.CreateAuthenticatedClient(token);
        var monday = PreviousWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        for (var i = 0; i < entryCount; i++)
            await CreateManualEntryAsync(client, monday.AddHours(9 + 2 * i));

        var submit = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = PreviousWeek });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var timesheet = await submit.Content.ReadFromJsonAsync<TimesheetResponse>();
        return new SubmittedWeek(timesheet!.Id, token);
    }

    private static async Task CreateManualEntryAsync(HttpClient client, DateTime startedAtUtc)
    {
        var response = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Logged work",
            startedAtUtc,
            endedAtUtc = startedAtUtc.AddHours(1)
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class PagedResponse<T>
    {
        public List<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    private sealed class AdminListItemResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? UserDisplayName { get; set; }
        public string UserEmail { get; set; } = "";
        public DateOnly WeekStartDate { get; set; }
        public string Status { get; set; } = "";
        public DateTime SubmittedAtUtc { get; set; }
        public long TotalSeconds { get; set; }
        public int EntryCount { get; set; }
    }

    private sealed class AdminDetailResponse
    {
        public required TimesheetResponse Timesheet { get; set; }
        public string? UserDisplayName { get; set; }
        public string UserEmail { get; set; } = "";
        public List<TimesheetEntryResponse> Entries { get; set; } = [];
        public long TotalSeconds { get; set; }
        public long BillableSeconds { get; set; }
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Timesheets;

public class TimesheetEndpointsTests
{
    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);

    [Fact]
    public async Task Submit_WeekWithLoggedTime_CreatesSubmittedTimesheet()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        await SeedManualEntryAsync(client, CurrentWeek);

        var response = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TimesheetResponse>();
        Assert.Equal("Submitted", body!.Status);
        Assert.Equal(CurrentWeek, body.WeekStartDate);
        Assert.Null(body.ReviewedByUserId);
    }

    [Fact]
    public async Task Submit_NonMondayWeekStart_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/timesheets/my/submit",
            new { weekStart = CurrentWeek.AddDays(1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_FutureWeek_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/timesheets/my/submit",
            new { weekStart = CurrentWeek.AddDays(7) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_EmptyWeek_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_RunningTimer_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        await SeedManualEntryAsync(client, CurrentWeek);
        var timer = await client.PostAsJsonAsync("/api/time-entries/timer/start", new { description = "Running" });
        Assert.Equal(HttpStatusCode.OK, timer.StatusCode);

        var response = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Submit_PendingSharedEntry_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var monday = CurrentWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var share = await adminClient.PostAsJsonAsync("/api/time-entries/shared", new
        {
            assigneeUserIds = new[] { member.Id },
            description = "Shared work",
            startedAtUtc = monday.AddHours(9),
            endedAtUtc = monday.AddHours(10)
        });
        Assert.Equal(HttpStatusCode.OK, share.StatusCode);

        var response = await memberClient.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Submit_Twice_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        await SeedManualEntryAsync(client, CurrentWeek);

        var first = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Submit_AfterRejection_ReusesRowAndClearsReviewerFields()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        await SeedManualEntryAsync(client, CurrentWeek);

        var first = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });
        var submitted = await first.Content.ReadFromJsonAsync<TimesheetResponse>();
        await RejectViaDbAsync(factory, submitted!.Id, "Please fix Tuesday.");

        var resubmit = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });

        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
        var body = await resubmit.Content.ReadFromJsonAsync<TimesheetResponse>();
        Assert.Equal(submitted.Id, body!.Id);
        Assert.Equal("Submitted", body.Status);
        Assert.Null(body.ReviewedByUserId);
        Assert.Null(body.ReviewedAtUtc);
        Assert.Null(body.ReviewComment);
    }

    [Fact]
    public async Task GetMyWeek_EmptyWeek_ReportsBlockersAndNoTimesheet()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync($"/api/timesheets/my/week?weekStart={CurrentWeek:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MyWeekTimesheetResponse>();
        Assert.Null(body!.Timesheet);
        Assert.Empty(body.Entries);
        Assert.False(body.CanSubmit);
        Assert.Contains(body.Blockers, b => b.Contains("no time logged", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetMyWeek_SubmittableWeek_ListsEntriesAndAllowsSubmit()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        await SeedManualEntryAsync(client, CurrentWeek, "Design review");

        var response = await client.GetAsync($"/api/timesheets/my/week?weekStart={CurrentWeek:yyyy-MM-dd}");

        var body = await response.Content.ReadFromJsonAsync<MyWeekTimesheetResponse>();
        Assert.True(body!.CanSubmit);
        Assert.Empty(body.Blockers);
        var entry = Assert.Single(body.Entries);
        Assert.Equal("Design review", entry.Description);
        Assert.Equal(3600, entry.DurationSeconds);
        Assert.Null(entry.ProjectName);
    }

    [Fact]
    public async Task GetMyWeek_NonMonday_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync($"/api/timesheets/my/week?weekStart={CurrentWeek.AddDays(2):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Withdraw_SubmittedTimesheet_DeletesRowAndUnlocksWeek()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        await SeedManualEntryAsync(client, CurrentWeek);
        var submit = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });
        var submitted = await submit.Content.ReadFromJsonAsync<TimesheetResponse>();

        var withdraw = await client.PostAsync($"/api/timesheets/{submitted!.Id}/withdraw", null);

        Assert.Equal(HttpStatusCode.NoContent, withdraw.StatusCode);
        var week = await client.GetFromJsonAsync<MyWeekTimesheetResponse>(
            $"/api/timesheets/my/week?weekStart={CurrentWeek:yyyy-MM-dd}");
        Assert.Null(week!.Timesheet);
        Assert.True(week.CanSubmit);

        // Week is editable again.
        var entry = await SeedManualEntryAsync(client, CurrentWeek, "After withdraw", startHour: 12);
        Assert.Equal(HttpStatusCode.OK, entry.StatusCode);
    }

    [Fact]
    public async Task Withdraw_ApprovedTimesheet_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        await SeedManualEntryAsync(client, CurrentWeek);
        var submit = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });
        var submitted = await submit.Content.ReadFromJsonAsync<TimesheetResponse>();
        await SetStatusViaDbAsync(factory, submitted!.Id, TimesheetStatus.Approved);

        var withdraw = await client.PostAsync($"/api/timesheets/{submitted.Id}/withdraw", null);

        Assert.Equal(HttpStatusCode.Conflict, withdraw.StatusCode);
    }

    [Fact]
    public async Task Withdraw_AnotherUsersTimesheet_ReturnsNotFound()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var (_, memberToken) = await factory.SeedMemberAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);
        await SeedManualEntryAsync(adminClient, CurrentWeek);
        var submit = await adminClient.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });
        var submitted = await submit.Content.ReadFromJsonAsync<TimesheetResponse>();

        var withdraw = await memberClient.PostAsync($"/api/timesheets/{submitted!.Id}/withdraw", null);

        Assert.Equal(HttpStatusCode.NotFound, withdraw.StatusCode);
    }

    [Fact]
    public async Task GetRecentWeeks_AggregatesTotalsStatusesAndGaps()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var previousWeek = CurrentWeek.AddDays(-7);

        // Current week: 1h billable + 30m non-billable. Previous week: 1h billable, submitted.
        await SeedManualEntryAsync(client, CurrentWeek);
        await SeedManualEntryAsync(client, CurrentWeek, "Internal", startHour: 12, durationMinutes: 30, isBillable: false);
        await SeedManualEntryAsync(client, previousWeek);
        await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = previousWeek });

        var response = await client.GetAsync("/api/timesheets/my/recent?count=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var weeks = await response.Content.ReadFromJsonAsync<List<WeekSummaryResponse>>();
        Assert.Equal(3, weeks!.Count);

        Assert.Equal(CurrentWeek, weeks[0].WeekStartDate);
        Assert.Equal(5400, weeks[0].TotalSeconds);
        Assert.Equal(3600, weeks[0].BillableSeconds);
        Assert.Equal("None", weeks[0].Status);
        Assert.Null(weeks[0].TimesheetId);

        Assert.Equal(previousWeek, weeks[1].WeekStartDate);
        Assert.Equal(3600, weeks[1].TotalSeconds);
        Assert.Equal("Submitted", weeks[1].Status);
        Assert.NotNull(weeks[1].TimesheetId);

        Assert.Equal(0, weeks[2].TotalSeconds);
        Assert.Equal("None", weeks[2].Status);
    }

    private static async Task<HttpResponseMessage> SeedManualEntryAsync(
        HttpClient client,
        DateOnly week,
        string description = "Logged work",
        int startHour = 9,
        int durationMinutes = 60,
        bool isBillable = true)
    {
        var monday = week.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var response = await client.PostAsJsonAsync("/api/time-entries", new
        {
            description,
            startedAtUtc = monday.AddHours(startHour),
            endedAtUtc = monday.AddHours(startHour).AddMinutes(durationMinutes),
            isBillable
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    private static Task RejectViaDbAsync(
        ReeTrackWebApplicationFactory factory,
        Guid timesheetId,
        string comment) =>
        SetStatusViaDbAsync(factory, timesheetId, TimesheetStatus.Rejected, comment);

    private static async Task SetStatusViaDbAsync(
        ReeTrackWebApplicationFactory factory,
        Guid timesheetId,
        TimesheetStatus status,
        string? comment = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var timesheet = await db.Timesheets.SingleAsync(t => t.Id == timesheetId);
        timesheet.Status = status;
        timesheet.ReviewedAtUtc = DateTime.UtcNow;
        timesheet.ReviewComment = comment;
        await db.SaveChangesAsync();
    }
}

internal sealed class TimesheetResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public string Status { get; set; } = "";
    public DateTime SubmittedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewComment { get; set; }
}

internal sealed class MyWeekTimesheetResponse
{
    public TimesheetResponse? Timesheet { get; set; }
    public List<TimesheetEntryResponse> Entries { get; set; } = [];
    public bool CanSubmit { get; set; }
    public List<string> Blockers { get; set; } = [];
}

internal sealed class TimesheetEntryResponse
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
    public string? ProjectName { get; set; }
    public string? ClientName { get; set; }
}

internal sealed class WeekSummaryResponse
{
    public DateOnly WeekStartDate { get; set; }
    public long TotalSeconds { get; set; }
    public long BillableSeconds { get; set; }
    public string Status { get; set; } = "";
    public Guid? TimesheetId { get; set; }
}

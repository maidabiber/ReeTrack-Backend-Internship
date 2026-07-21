using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Timesheets;

/// <summary>
/// A submitted week must reject every time-entry mutation with 409 until the
/// timesheet is rejected (or withdrawn), which reopens the week.
/// </summary>
public class TimesheetWeekLockTests
{
    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);
    private static DateOnly PreviousWeek => CurrentWeek.AddDays(-7);
    private static DateTime Monday(DateOnly week, int hour) =>
        week.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(hour);

    [Fact]
    public async Task SubmittedWeek_BlocksAllMutations_UntilRejected()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        // An entry in the previous week (gets locked) and one in the current week (stays editable).
        var lockedEntryId = await CreateManualEntryAsync(client, Monday(PreviousWeek, 9));
        var editableEntryId = await CreateManualEntryAsync(client, Monday(CurrentWeek, 9));

        var submit = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = PreviousWeek });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var timesheet = await submit.Content.ReadFromJsonAsync<TimesheetResponse>();

        // Manual create in the locked week.
        var manual = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Late addition",
            startedAtUtc = Monday(PreviousWeek, 14),
            endedAtUtc = Monday(PreviousWeek, 15)
        });
        Assert.Equal(HttpStatusCode.Conflict, manual.StatusCode);

        // Duration-only create anchored in the locked week.
        var duration = await client.PostAsJsonAsync("/api/time-entries/duration", new
        {
            description = "Late duration",
            entryDateUtc = Monday(PreviousWeek, 0),
            durationSeconds = 1800
        });
        Assert.Equal(HttpStatusCode.Conflict, duration.StatusCode);

        // Editing an entry that lives in the locked week.
        var editLocked = await client.PutAsJsonAsync($"/api/time-entries/{lockedEntryId}", new
        {
            description = "Tweak",
            startedAtUtc = Monday(PreviousWeek, 9),
            endedAtUtc = Monday(PreviousWeek, 11)
        });
        Assert.Equal(HttpStatusCode.Conflict, editLocked.StatusCode);

        // Moving an editable entry INTO the locked week.
        var moveIn = await client.PutAsJsonAsync($"/api/time-entries/{editableEntryId}", new
        {
            description = "Moved back",
            startedAtUtc = Monday(PreviousWeek, 16),
            endedAtUtc = Monday(PreviousWeek, 17)
        });
        Assert.Equal(HttpStatusCode.Conflict, moveIn.StatusCode);

        // The current week is untouched by the lock.
        var editCurrent = await client.PutAsJsonAsync($"/api/time-entries/{editableEntryId}", new
        {
            description = "Still editable",
            startedAtUtc = Monday(CurrentWeek, 9),
            endedAtUtc = Monday(CurrentWeek, 11)
        });
        Assert.Equal(HttpStatusCode.OK, editCurrent.StatusCode);

        // Rejection reopens the week.
        await SetStatusViaDbAsync(factory, timesheet!.Id, TimesheetStatus.Rejected);
        var afterReject = await client.PutAsJsonAsync($"/api/time-entries/{lockedEntryId}", new
        {
            description = "Fixed after rejection",
            startedAtUtc = Monday(PreviousWeek, 9),
            endedAtUtc = Monday(PreviousWeek, 11)
        });
        Assert.Equal(HttpStatusCode.OK, afterReject.StatusCode);
    }

    [Fact]
    public async Task SubmittedCurrentWeek_BlocksStartingTimer()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        await CreateManualEntryAsync(client, Monday(CurrentWeek, 9));
        var submit = await client.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = CurrentWeek });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var timer = await client.PostAsJsonAsync("/api/time-entries/timer/start", new { description = "Nope" });

        Assert.Equal(HttpStatusCode.Conflict, timer.StatusCode);
    }

    [Fact]
    public async Task SharedEntryIntoAssigneesLockedWeek_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        // Member submits the previous week; the admin's own week is not locked.
        await CreateManualEntryAsync(memberClient, Monday(PreviousWeek, 9));
        var submit = await memberClient.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = PreviousWeek });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var share = await adminClient.PostAsJsonAsync("/api/time-entries/shared/manual", new
        {
            assigneeUserIds = new[] { member.Id },
            description = "Into locked week",
            startedAtUtc = Monday(PreviousWeek, 14),
            endedAtUtc = Monday(PreviousWeek, 15)
        });

        Assert.Equal(HttpStatusCode.Conflict, share.StatusCode);
    }

    [Fact]
    public async Task ApprovingPendingEntryInLockedWeek_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var (member, memberToken) = await factory.SeedMemberAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        // Admin shares an entry in the member's previous week while it is still open.
        var share = await adminClient.PostAsJsonAsync("/api/time-entries/shared/manual", new
        {
            assigneeUserIds = new[] { member.Id },
            description = "Pending work",
            startedAtUtc = Monday(PreviousWeek, 9),
            endedAtUtc = Monday(PreviousWeek, 10)
        });
        Assert.Equal(HttpStatusCode.OK, share.StatusCode);

        // Pending entries block submission, so approve this one, submit the week,
        // then verify a later pending entry cannot be approved in the locked week.
        var approveBeforeLock = await ApproveFirstPendingAsync(memberClient);
        Assert.Equal(HttpStatusCode.OK, approveBeforeLock.StatusCode);

        var submit = await memberClient.PostAsJsonAsync("/api/timesheets/my/submit", new { weekStart = PreviousWeek });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        // A pending clone cannot even be created in the locked week (per-assignee guard),
        // so seed one directly to prove approval is also guarded.
        await SeedPendingEntryViaDbAsync(factory, member.Id, Monday(PreviousWeek, 16));
        var approveAfterLock = await ApproveFirstPendingAsync(memberClient);
        Assert.Equal(HttpStatusCode.Conflict, approveAfterLock.StatusCode);
    }

    private static async Task<Guid> CreateManualEntryAsync(HttpClient client, DateTime startedAtUtc)
    {
        var response = await client.PostAsJsonAsync("/api/time-entries/manual", new
        {
            description = "Logged work",
            startedAtUtc,
            endedAtUtc = startedAtUtc.AddHours(1)
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateEntryEnvelope>();
        return body!.Entry.Id;
    }

    private static async Task<HttpResponseMessage> ApproveFirstPendingAsync(HttpClient client)
    {
        var pending = await client.GetFromJsonAsync<List<TimesheetEntryResponse>>("/api/time-entries/pending");
        Assert.NotEmpty(pending!);
        return await client.PostAsync($"/api/time-entries/pending/{pending![0].Id}/approve", null);
    }

    private static async Task SeedPendingEntryViaDbAsync(
        ReeTrackWebApplicationFactory factory,
        Guid assigneeUserId,
        DateTime startedAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TimeEntries.Add(new TimeEntry
        {
            UserId = assigneeUserId,
            Description = "Seeded pending",
            Mode = TimeEntryMode.Manual,
            Status = TimeEntryStatus.Pending,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = startedAtUtc.AddHours(1),
            DurationSeconds = 3600
        });
        await db.SaveChangesAsync();
    }

    private static async Task SetStatusViaDbAsync(
        ReeTrackWebApplicationFactory factory,
        Guid timesheetId,
        TimesheetStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var timesheet = await db.Timesheets.SingleAsync(t => t.Id == timesheetId);
        timesheet.Status = status;
        await db.SaveChangesAsync();
    }

    private sealed class CreateEntryEnvelope
    {
        public required TimesheetEntryResponse Entry { get; set; }
    }
}

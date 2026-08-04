using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Reports;

public class ReportFilteringEndpointsTests
{
    [Fact]
    public async Task GetSummary_AllFilters_ReturnsOnlyMatchingEntries()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var (member, _) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        Guid clientId;
        Guid projectId;
        Guid taskId;
        Guid tagId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            var otherCustomer = new Client { Name = "Other" };
            db.Clients.AddRange(customer, otherCustomer);
            await db.SaveChangesAsync();

            var project = Project(customer.Id, admin.Id, "Matched");
            var otherProject = Project(otherCustomer.Id, admin.Id, "Other");
            db.Projects.AddRange(project, otherProject);
            await db.SaveChangesAsync();

            var task = new ProjectTask
            {
                ProjectId = project.Id,
                Name = "Audit",
                Status = ProjectTaskStatus.Open
            };
            var tag = new Tag { Name = "compliance" };
            db.ProjectTasks.Add(task);
            db.Tags.Add(tag);
            await db.SaveChangesAsync();

            var matching = Entry(member.Id, customer.Id, project.Id, task.Id, date, true, 3600);
            matching.TimeEntryTags.Add(new TimeEntryTag { TagId = tag.Id });
            db.TimeEntries.AddRange(
                matching,
                Entry(admin.Id, customer.Id, project.Id, task.Id, date, true, 7200),
                Entry(member.Id, otherCustomer.Id, otherProject.Id, null, date, false, 10800));
            await db.SaveChangesAsync();

            clientId = customer.Id;
            projectId = project.Id;
            taskId = task.Id;
            tagId = tag.Id;
        }

        var url = "/api/reports/summary"
            + $"?userIds={member.Id}"
            + $"&projectIds={projectId}"
            + $"&clientIds={clientId}"
            + $"&taskIds={taskId}"
            + $"&tagIds={tagId}"
            + "&billable=true"
            + $"&from={date:yyyy-MM-dd}"
            + $"&to={date:yyyy-MM-dd}"
            + "&groupBy=user&groupBy=project";

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<SummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary.Kpis.EntryCount);
        Assert.Equal(3600, summary.Kpis.TotalSeconds);
        Assert.Equal(date, summary.FilterFromDate);
        Assert.Equal(date, summary.FilterToDate);
        Assert.Equal("Matched", Assert.Single(summary.Projects).Name);
        Assert.Equal(member.Id, Assert.Single(summary.Members).UserId);
    }

    [Fact]
    public async Task GetSummary_ProjectFilter_PreservesCrossProjectOvertimeContext()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var (member, _) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var monday = TimesheetWeek.ToWeekStart(DateTime.UtcNow);

        Guid selectedProjectId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();

            var contextProject = Project(customer.Id, admin.Id, "Context");
            var selectedProject = Project(customer.Id, admin.Id, "Selected");
            db.Projects.AddRange(contextProject, selectedProject);
            await db.SaveChangesAsync();

            db.TimeEntries.AddRange(
                Entry(member.Id, customer.Id, contextProject.Id, null, monday, true, 40 * 3600),
                Entry(member.Id, customer.Id, selectedProject.Id, null, monday.AddDays(1), true, 3600));
            await db.SaveChangesAsync();
            selectedProjectId = selectedProject.Id;
        }

        var summary = await client.GetFromJsonAsync<SummaryResponse>(
            $"/api/reports/summary?projectIds={selectedProjectId}");

        Assert.NotNull(summary);
        var project = Assert.Single(summary.Projects);
        Assert.Equal(1m, project.OvertimeHours);
        Assert.True(project.CalculatedCost > 12.82m);
    }

    [Fact]
    public async Task GetSummary_InactiveHoliday_DoesNotApplyHolidayPremium()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var nextMonday = TimesheetWeek.ToWeekStart(DateTime.UtcNow).AddDays(7);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();

            var project = Project(customer.Id, admin.Id, "Standard day");
            db.Projects.Add(project);
            db.Holidays.Add(new Holiday
            {
                Date = nextMonday,
                Name = "Inactive holiday",
                IsActive = false
            });
            await db.SaveChangesAsync();

            db.TimeEntries.Add(Entry(
                admin.Id,
                customer.Id,
                project.Id,
                null,
                nextMonday,
                true,
                3600));
            await db.SaveChangesAsync();
        }

        var summary = await client.GetFromJsonAsync<SummaryResponse>("/api/reports/summary");

        Assert.NotNull(summary);
        Assert.Equal(12.82m, Assert.Single(summary.Projects).CalculatedCost);
    }

    [Fact]
    public async Task GetSummary_AsProjectManager_OnlyShowsOwnProjects()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (pm, pmToken) = await factory.SeedProjectManagerAsync();
        var pmClient = factory.CreateAuthenticatedClient(pmToken);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();

            var pmProject = Project(customer.Id, pm.Id, "PM project");
            var otherProject = Project(customer.Id, Guid.NewGuid(), "Other project");
            db.Projects.AddRange(pmProject, otherProject);
            await db.SaveChangesAsync();

            db.TimeEntries.AddRange(
                Entry(pm.Id, customer.Id, pmProject.Id, null, date, true, 3600),
                Entry(pm.Id, customer.Id, otherProject.Id, null, date, true, 7200));
            await db.SaveChangesAsync();
        }

        var summary = await pmClient.GetFromJsonAsync<SummaryResponse>("/api/reports/summary");

        Assert.NotNull(summary);
        Assert.Equal(1, summary.Kpis.EntryCount);
        Assert.Equal(3600, summary.Kpis.TotalSeconds);
        var project = Assert.Single(summary.Projects);
        Assert.Equal("PM project", project.Name);
    }

    [Fact]
    public async Task GetSummary_AsProjectManager_NoOwnProjects_ReturnsEmpty()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (pm, pmToken) = await factory.SeedProjectManagerAsync();
        var pmClient = factory.CreateAuthenticatedClient(pmToken);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();

            var otherProject = Project(customer.Id, Guid.NewGuid(), "Not mine");
            db.Projects.Add(otherProject);
            await db.SaveChangesAsync();

            db.TimeEntries.Add(Entry(pm.Id, customer.Id, otherProject.Id, null, date, true, 3600));
            await db.SaveChangesAsync();
        }

        var summary = await pmClient.GetFromJsonAsync<SummaryResponse>("/api/reports/summary");

        Assert.NotNull(summary);
        Assert.Equal(0, summary.Kpis.EntryCount);
        Assert.Empty(summary.Projects);
    }

    [Fact]
    public async Task GetSummary_AsAdmin_SeesAllProjects()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();

            var project1 = Project(customer.Id, admin.Id, "Admin project");
            var project2 = Project(customer.Id, Guid.NewGuid(), "Other project");
            db.Projects.AddRange(project1, project2);
            await db.SaveChangesAsync();

            db.TimeEntries.AddRange(
                Entry(admin.Id, customer.Id, project1.Id, null, date, true, 3600),
                Entry(admin.Id, customer.Id, project2.Id, null, date, true, 7200));
            await db.SaveChangesAsync();
        }

        var summary = await adminClient.GetFromJsonAsync<SummaryResponse>("/api/reports/summary");

        Assert.NotNull(summary);
        Assert.Equal(2, summary.Kpis.EntryCount);
        Assert.Equal(2, summary.Projects.Count);
    }

    [Theory]
    [InlineData("/api/reports/summary?from=2026-07-02&to=2026-07-01")]
    [InlineData("/api/reports/summary?groupBy=unsupported")]
    public async Task GetSummary_InvalidQuery_ReturnsBadRequest(string url)
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Project Project(Guid clientId, Guid creatorId, string name) =>
        new()
        {
            ClientId = clientId,
            CreatedByUserId = creatorId,
            Name = name,
            Status = ProjectStatus.Active,
            CurrencyCode = "EUR",
            HourlyRate = 10m
        };

    private static TimeEntry Entry(
        Guid userId,
        Guid clientId,
        Guid projectId,
        Guid? taskId,
        DateOnly date,
        bool billable,
        int durationSeconds)
    {
        var startedAtUtc = date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        return new TimeEntry
        {
            UserId = userId,
            ClientId = clientId,
            ProjectId = projectId,
            ProjectTaskId = taskId,
            IsBillable = billable,
            Mode = TimeEntryMode.Manual,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = startedAtUtc.AddSeconds(durationSeconds),
            DurationSeconds = durationSeconds,
            Status = TimeEntryStatus.Confirmed
        };
    }

    private sealed class SummaryResponse
    {
        public KpisResponse Kpis { get; init; } = new();
        public IReadOnlyList<ProjectResponse> Projects { get; init; } = [];
        public IReadOnlyList<MemberResponse> Members { get; init; } = [];
        public DateOnly? FilterFromDate { get; init; }
        public DateOnly? FilterToDate { get; init; }
    }

    private sealed class KpisResponse
    {
        public int EntryCount { get; init; }
        public long TotalSeconds { get; init; }
    }

    private sealed class ProjectResponse
    {
        public string Name { get; init; } = string.Empty;
        public decimal CalculatedCost { get; init; }
        public decimal OvertimeHours { get; init; }
    }

    private sealed class MemberResponse
    {
        public Guid UserId { get; init; }
    }
}

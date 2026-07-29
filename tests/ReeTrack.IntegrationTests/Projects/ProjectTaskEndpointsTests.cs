using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Projects;

/// <summary>
/// Covers /api/projects/{projectId}/tasks: nested CRUD, per-project name
/// uniqueness, assignee resolution, status toggles, the mismatched-project 404,
/// and the delete-with-tracked-time guard.
/// </summary>
public class ProjectTaskEndpointsTests
{
    [Fact]
    public async Task Create_ReturnsTask_AndListIncludesIt()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectId = await SeedProjectAsync(factory, "Redesign");

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { name = "  Design  " });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.Equal("Design", created!.Name);
        Assert.Equal("open", created.Status);

        var list = await client.GetFromJsonAsync<PagedResult<TaskResponse>>($"/api/projects/{projectId}/tasks");
        Assert.Single(list!.Items);
    }

    [Fact]
    public async Task Create_UnknownProject_Returns404()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/tasks", new { name = "Design" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_BlankName_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectId = await SeedProjectAsync(factory, "Redesign");

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateInProject_Returns409_ButSameNameOtherProjectOk()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectA = await SeedProjectAsync(factory, "Project A");
        var projectB = await SeedProjectAsync(factory, "Project B");

        (await client.PostAsJsonAsync($"/api/projects/{projectA}/tasks", new { name = "Design" })).EnsureSuccessStatusCode();

        var dup = await client.PostAsJsonAsync($"/api/projects/{projectA}/tasks", new { name = "design" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        var otherProject = await client.PostAsJsonAsync($"/api/projects/{projectB}/tasks", new { name = "Design" });
        Assert.Equal(HttpStatusCode.OK, otherProject.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownAssignee_Returns400_AndValidAssignee_ReturnsName()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectId = await SeedProjectAsync(factory, "Redesign");

        var unknown = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new
        {
            name = "Design",
            assignedToUserId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var assigned = await (await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new
        {
            name = "Build",
            assignedToUserId = admin.Id
        })).Content.ReadFromJsonAsync<TaskResponse>();

        Assert.Equal(admin.Id, assigned!.AssignedToUserId);
        Assert.False(string.IsNullOrEmpty(assigned.AssignedToName));
    }

    [Fact]
    public async Task Create_NegativeEstimate_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectId = await SeedProjectAsync(factory, "Redesign");

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new
        {
            name = "Design",
            timeEstimateHours = -3
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Mutations_AsMember_Succeed()
    {
        // Trust-based domain: members (not just admins) may create/edit/delete tasks.
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var projectId = await SeedProjectAsync(factory, "Redesign");

        var created = await (await adminClient.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { name = "Design" }))
            .Content.ReadFromJsonAsync<TaskResponse>();

        var memberToken = await SeedMemberTokenAsync(factory);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync($"/api/projects/{projectId}/tasks")).StatusCode);

        var create = await memberClient.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { name = "Member task" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var memberTask = await create.Content.ReadFromJsonAsync<TaskResponse>();

        var patch = await memberClient.PatchAsJsonAsync($"/api/projects/{projectId}/tasks/{created!.Id}", new { status = "done" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var delete = await memberClient.DeleteAsync($"/api/projects/{projectId}/tasks/{memberTask!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Patch_StatusToggle_And_ContentUpdateClearsFields()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectId = await SeedProjectAsync(factory, "Redesign");

        var created = await (await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new
        {
            name = "Design",
            assignedToUserId = admin.Id,
            timeEstimateHours = 8
        })).Content.ReadFromJsonAsync<TaskResponse>();

        var done = await (await client.PatchAsJsonAsync($"/api/projects/{projectId}/tasks/{created!.Id}", new { status = "done" }))
            .Content.ReadFromJsonAsync<TaskResponse>();
        Assert.Equal("done", done!.Status);
        Assert.Equal(admin.Id, done.AssignedToUserId); // status-only patch keeps content

        var cleared = await (await client.PatchAsJsonAsync($"/api/projects/{projectId}/tasks/{created.Id}", new { name = "Design v2" }))
            .Content.ReadFromJsonAsync<TaskResponse>();
        Assert.Equal("Design v2", cleared!.Name);
        Assert.Null(cleared.AssignedToUserId); // content update with omitted fields clears them
        Assert.Null(cleared.TimeEstimateHours);
    }

    [Fact]
    public async Task Patch_MismatchedProject_Returns404()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectA = await SeedProjectAsync(factory, "Project A");
        var projectB = await SeedProjectAsync(factory, "Project B");

        var created = await (await client.PostAsJsonAsync($"/api/projects/{projectA}/tasks", new { name = "Design" }))
            .Content.ReadFromJsonAsync<TaskResponse>();

        var mismatched = await client.PatchAsJsonAsync($"/api/projects/{projectB}/tasks/{created!.Id}", new { status = "done" });
        Assert.Equal(HttpStatusCode.NotFound, mismatched.StatusCode);
    }

    [Fact]
    public async Task Delete_WithTrackedTime_Returns409_Otherwise_SoftDeletes()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectId = await SeedProjectAsync(factory, "Redesign");

        var guarded = await (await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { name = "Tracked" }))
            .Content.ReadFromJsonAsync<TaskResponse>();
        await SeedTimeEntryAsync(factory, admin.Id, projectId, guarded!.Id);

        var blocked = await client.DeleteAsync($"/api/projects/{projectId}/tasks/{guarded.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var free = await (await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { name = "Free" }))
            .Content.ReadFromJsonAsync<TaskResponse>();

        var deleted = await client.DeleteAsync($"/api/projects/{projectId}/tasks/{free!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var list = await client.GetFromJsonAsync<PagedResult<TaskResponse>>($"/api/projects/{projectId}/tasks");
        Assert.DoesNotContain(list!.Items, t => t.Id == free.Id);
    }

    [Fact]
    public async Task List_PaginationAndSearch_Work()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectId = await SeedProjectAsync(factory, "Redesign");

        for (var i = 1; i <= 3; i++)
        {
            (await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { name = $"Task {i}" }))
                .EnsureSuccessStatusCode();
        }

        var page1 = await client.GetFromJsonAsync<PagedResult<TaskResponse>>(
            $"/api/projects/{projectId}/tasks?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(3, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);

        var page2 = await client.GetFromJsonAsync<PagedResult<TaskResponse>>(
            $"/api/projects/{projectId}/tasks?page=2&pageSize=2");
        Assert.Single(page2!.Items);

        var filtered = await client.GetFromJsonAsync<PagedResult<TaskResponse>>(
            $"/api/projects/{projectId}/tasks?q=task%202");
        Assert.Equal(1, filtered!.TotalCount);
        Assert.Equal("Task 2", Assert.Single(filtered.Items).Name);
    }

    [Fact]
    public async Task ListAcrossProjects_StatusFilter_IncludesCompletedTasksForReports()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectId = await SeedProjectAsync(factory, "Redesign");
        var open = await (await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { name = "Open task" })).Content.ReadFromJsonAsync<TaskResponse>();
        var done = await (await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { name = "Done task" })).Content.ReadFromJsonAsync<TaskResponse>();
        await client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/tasks/{done!.Id}",
            new { status = "done" });

        var defaultOpen = await client.GetFromJsonAsync<PagedResult<TaskResponse>>("/api/tasks");
        var all = await client.GetFromJsonAsync<PagedResult<TaskResponse>>("/api/tasks?status=all");
        var completed = await client.GetFromJsonAsync<PagedResult<TaskResponse>>("/api/tasks?status=done");

        Assert.Equal(open!.Id, Assert.Single(defaultOpen!.Items).Id);
        Assert.Equal(2, all!.TotalCount);
        Assert.Equal(done.Id, Assert.Single(completed!.Items).Id);
    }

    [Fact]
    public async Task ListAcrossProjects_InvalidStatus_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/tasks?status=unknown");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListAcrossProjects_ProjectIds_FiltersAndReturnsClientId()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var projectA = await SeedProjectAsync(factory, "Project A");
        var projectB = await SeedProjectAsync(factory, "Project B");

        var taskA = await (await client.PostAsJsonAsync(
            $"/api/projects/{projectA}/tasks",
            new { name = "Task A" })).Content.ReadFromJsonAsync<TaskResponse>();
        await client.PostAsJsonAsync($"/api/projects/{projectB}/tasks", new { name = "Task B" });

        var result = await client.GetFromJsonAsync<PagedResult<TaskResponse>>(
            $"/api/tasks?status=all&projectIds={projectA}");

        var listed = Assert.Single(result!.Items);
        Assert.Equal(taskA!.Id, listed.Id);
        Assert.Equal(projectA, listed.ProjectId);
        Assert.True(listed.ClientId != Guid.Empty);
        Assert.Equal("Project A", listed.ProjectName);
        Assert.Equal("#4366E2", listed.ProjectColor);
    }

    private static async Task<Guid> SeedProjectAsync(ReeTrackWebApplicationFactory factory, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = new Client { Name = $"Client for {name}" };
        db.Clients.Add(client);
        var project = new Project
        {
            Client = client,
            Name = name,
            Color = "#4366E2",
            Status = ProjectStatus.Active
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static async Task SeedTimeEntryAsync(
        ReeTrackWebApplicationFactory factory,
        Guid userId,
        Guid projectId,
        Guid projectTaskId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TimeEntries.Add(new TimeEntry
        {
            UserId = userId,
            ProjectId = projectId,
            ProjectTaskId = projectTaskId,
            Mode = TimeEntryMode.Manual,
            DurationSeconds = 3600
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string> SeedMemberTokenAsync(ReeTrackWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var now = DateTime.UtcNow;

        var member = new User
        {
            Email = "member@reetrack.test",
            DisplayName = "Test Member",
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UserRoles = [new UserRole { RoleId = RoleIds.Member, AssignedAtUtc = now }]
        };

        db.Users.Add(member);
        await db.SaveChangesAsync();

        return jwt.CreateAccessToken(member, ["Member"], out _);
    }

    private sealed class TaskResponse
    {
        public Guid Id { get; init; }
        public Guid ProjectId { get; init; }
        public Guid ClientId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string? ProjectName { get; init; }
        public string? ProjectColor { get; init; }
        public string? ClientName { get; init; }
        public Guid? AssignedToUserId { get; init; }
        public string? AssignedToName { get; init; }
        public decimal? TimeEstimateHours { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}

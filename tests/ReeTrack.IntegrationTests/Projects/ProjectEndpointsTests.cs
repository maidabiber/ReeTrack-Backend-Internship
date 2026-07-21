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
/// Covers /api/projects: CRUD, billing-block semantics, status/client filters,
/// admin-vs-member authorization, and the delete-with-tracked-time guard plus
/// cascade soft-delete of tasks. Each test gets its own factory and database.
/// </summary>
public class ProjectEndpointsTests
{
    [Fact]
    public async Task Create_ReturnsProject_WithDefaults()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");

        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            name = "  Website Redesign  ",
            clientId
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(created);
        Assert.Equal("Website Redesign", created.Name);
        Assert.Equal("active", created.Status);
        Assert.Equal("hourly", created.BillingType);
        Assert.Equal("EUR", created.CurrencyCode);
        Assert.Equal("Acme Corp", created.ClientName);
        Assert.Equal(0, created.TaskCount);

        var list = await client.GetFromJsonAsync<PagedResult<ProjectResponse>>("/api/projects");
        var listed = Assert.Single(list!.Items);
        Assert.Equal(created.Id, listed.Id);
    }

    [Fact]
    public async Task Create_BlankName_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");

        var response = await client.PostAsJsonAsync("/api/projects", new { name = "   ", clientId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownClient_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            name = "Orphan",
            clientId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409_CaseInsensitive()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");

        var first = await client.PostAsJsonAsync("/api/projects", new { name = "Redesign", clientId });
        first.EnsureSuccessStatusCode();

        var dup = await client.PostAsJsonAsync("/api/projects", new { name = "redesign", clientId });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Theory]
    [InlineData("color", "red")]
    [InlineData("currencyCode", "EUROS")]
    [InlineData("hourlyRate", -5)]
    [InlineData("budgetAmount", -1)]
    public async Task Create_InvalidField_Returns400(string field, object value)
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");

        var payload = new Dictionary<string, object>
        {
            ["name"] = "Project X",
            ["clientId"] = clientId,
            ["billingType"] = "hourly",
            [field] = value
        };

        var response = await client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_BillingType_KeepsFeeModelsExclusive()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");

        var hourly = await (await client.PostAsJsonAsync("/api/projects", new
        {
            name = "Hourly Project",
            clientId,
            billingType = "hourly",
            hourlyRate = 90,
            fixedFeeAmount = 12000
        })).Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.Equal(90, hourly!.HourlyRate);
        Assert.Null(hourly.FixedFeeAmount);

        var fixedFee = await (await client.PostAsJsonAsync("/api/projects", new
        {
            name = "Fixed Project",
            clientId,
            billingType = "fixedFee",
            hourlyRate = 90,
            fixedFeeAmount = 12000
        })).Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.Equal(12000, fixedFee!.FixedFeeAmount);
        Assert.Null(fixedFee.HourlyRate);
    }

    [Fact]
    public async Task Mutations_AsMember_Succeed()
    {
        // Trust-based domain: members (not just admins) may create/edit/delete projects.
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var clientId = await SeedClientAsync(factory, "Acme Corp");

        var created = await (await adminClient.PostAsJsonAsync("/api/projects", new { name = "Redesign", clientId }))
            .Content.ReadFromJsonAsync<ProjectResponse>();

        var memberToken = await SeedMemberTokenAsync(factory);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync("/api/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync($"/api/projects/{created!.Id}")).StatusCode);

        var create = await memberClient.PostAsJsonAsync("/api/projects", new { name = "Member project", clientId });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var memberProject = await create.Content.ReadFromJsonAsync<ProjectResponse>();

        var patch = await memberClient.PatchAsJsonAsync($"/api/projects/{created.Id}", new { status = "archived" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var delete = await memberClient.DeleteAsync($"/api/projects/{memberProject!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task List_StatusAndClientFilters_Work()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var acme = await SeedClientAsync(factory, "Acme Corp");
        var globex = await SeedClientAsync(factory, "Globex");

        await SeedProjectAsync(factory, acme, "Acme Active");
        await SeedProjectAsync(factory, acme, "Acme Archived", ProjectStatus.Archived);
        await SeedProjectAsync(factory, globex, "Globex Active");

        var active = await client.GetFromJsonAsync<PagedResult<ProjectResponse>>("/api/projects");
        Assert.Equal(2, active!.TotalCount);
        Assert.Equal(2, active.Items.Count);

        var archived = await client.GetFromJsonAsync<PagedResult<ProjectResponse>>("/api/projects?status=archived");
        Assert.Equal("Acme Archived", Assert.Single(archived!.Items).Name);

        var all = await client.GetFromJsonAsync<PagedResult<ProjectResponse>>("/api/projects?status=all");
        Assert.Equal(3, all!.TotalCount);

        var byClient = await client.GetFromJsonAsync<PagedResult<ProjectResponse>>($"/api/projects?status=all&clientId={acme}");
        Assert.Equal(2, byClient!.TotalCount);
        Assert.All(byClient.Items, p => Assert.Equal(acme, p.ClientId));

        var invalid = await client.GetAsync("/api/projects?status=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task List_PaginationAndSearch_Work()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var acme = await SeedClientAsync(factory, "Acme Corp");

        for (var i = 1; i <= 3; i++)
            await SeedProjectAsync(factory, acme, $"Project {i}");

        var page1 = await client.GetFromJsonAsync<PagedResult<ProjectResponse>>("/api/projects?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(3, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);

        var page2 = await client.GetFromJsonAsync<PagedResult<ProjectResponse>>("/api/projects?page=2&pageSize=2");
        Assert.Single(page2!.Items);

        var filtered = await client.GetFromJsonAsync<PagedResult<ProjectResponse>>("/api/projects?q=project%202");
        Assert.Equal(1, filtered!.TotalCount);
        Assert.Equal("Project 2", Assert.Single(filtered.Items).Name);
    }

    [Fact]
    public async Task Get_ReturnsProject_AndUnknownId_Returns404()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");
        var projectId = await SeedProjectAsync(factory, clientId, "Redesign");

        var found = await client.GetFromJsonAsync<ProjectResponse>($"/api/projects/{projectId}");
        Assert.Equal("Redesign", found!.Name);

        var missing = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Patch_StatusOnly_LeavesBillingUntouched()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");

        var created = await (await client.PostAsJsonAsync("/api/projects", new
        {
            name = "Redesign",
            clientId,
            billingType = "hourly",
            hourlyRate = 90,
            budgetAmount = 5000,
            color = "#4366E2"
        })).Content.ReadFromJsonAsync<ProjectResponse>();

        var archived = await (await client.PatchAsJsonAsync($"/api/projects/{created!.Id}", new { status = "archived" }))
            .Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.Equal("archived", archived!.Status);
        Assert.Equal(90, archived.HourlyRate);
        Assert.Equal(5000, archived.BudgetAmount);
        Assert.Equal("#4366E2", archived.Color);
    }

    [Fact]
    public async Task Patch_BillingBlock_ClearsOmittedOptionalFields()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");

        var created = await (await client.PostAsJsonAsync("/api/projects", new
        {
            name = "Redesign",
            clientId,
            billingType = "hourly",
            hourlyRate = 90,
            budgetAmount = 5000,
            color = "#4366E2"
        })).Content.ReadFromJsonAsync<ProjectResponse>();

        // Re-send the billing block with only billingType: the wholesale rule clears budget/color.
        var updated = await (await client.PatchAsJsonAsync($"/api/projects/{created!.Id}", new
        {
            billingType = "hourly",
            hourlyRate = 100
        })).Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.Equal(100, updated!.HourlyRate);
        Assert.Null(updated.BudgetAmount);
        Assert.Null(updated.Color);
    }

    [Fact]
    public async Task Patch_RenameToExisting_Returns409()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");
        await SeedProjectAsync(factory, clientId, "Alpha");
        var beta = await SeedProjectAsync(factory, clientId, "Beta");

        var conflict = await client.PatchAsJsonAsync($"/api/projects/{beta}", new { name = "ALPHA" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutTrackedTime_SoftDeletes_AndCascadesTasks()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");
        var projectId = await SeedProjectAsync(factory, clientId, "Redesign");
        var taskId = await SeedTaskAsync(factory, projectId, "Design");

        var response = await client.DeleteAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{projectId}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.ProjectTasks.IgnoreQueryFilters().SingleAsync(t => t.Id == taskId);
        Assert.NotNull(task.DeletedAtUtc);
    }

    [Fact]
    public async Task Delete_WithTrackedTime_Returns409()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var clientId = await SeedClientAsync(factory, "Acme Corp");
        var projectId = await SeedProjectAsync(factory, clientId, "Redesign");
        await SeedTimeEntryAsync(factory, admin.Id, projectId: projectId);

        var response = await client.DeleteAsync($"/api/projects/{projectId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static async Task<Guid> SeedClientAsync(ReeTrackWebApplicationFactory factory, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = new Client { Name = name };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }

    private static async Task<Guid> SeedProjectAsync(
        ReeTrackWebApplicationFactory factory,
        Guid clientId,
        string name,
        ProjectStatus status = ProjectStatus.Active)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = new Project
        {
            ClientId = clientId,
            Name = name,
            Status = status,
            BillingType = BillingType.Hourly
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static async Task<Guid> SeedTaskAsync(ReeTrackWebApplicationFactory factory, Guid projectId, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = new ProjectTask
        {
            ProjectId = projectId,
            Name = name,
            Status = ProjectTaskStatus.Open
        };
        db.ProjectTasks.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    private static async Task SeedTimeEntryAsync(
        ReeTrackWebApplicationFactory factory,
        Guid userId,
        Guid? projectId = null,
        Guid? projectTaskId = null)
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

    private sealed class ProjectResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public Guid ClientId { get; init; }
        public string ClientName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string BillingType { get; init; } = string.Empty;
        public string CurrencyCode { get; init; } = string.Empty;
        public decimal? HourlyRate { get; init; }
        public decimal? FixedFeeAmount { get; init; }
        public decimal? BudgetAmount { get; init; }
        public decimal? TimeEstimateHours { get; init; }
        public string? Color { get; init; }
        public int TaskCount { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}

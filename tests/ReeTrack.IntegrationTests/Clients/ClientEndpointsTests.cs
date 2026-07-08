using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Clients;

/// <summary>
/// Covers /api/clients: CRUD, status filtering, project counts, the
/// delete-with-projects guard and soft-delete semantics. Each test gets its
/// own factory (and database) because list assertions depend on exactly which
/// clients exist.
/// </summary>
public class ClientEndpointsTests
{
    [Fact]
    public async Task Create_ReturnsClient_AndListIncludesIt()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/clients", new { name = "  Acme Corp  " });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.NotNull(created);
        Assert.Equal("Acme Corp", created.Name);
        Assert.True(created.IsActive);
        Assert.Equal(0, created.ProjectCount);

        var list = await client.GetFromJsonAsync<List<ClientResponse>>("/api/clients");
        Assert.NotNull(list);
        var listed = Assert.Single(list);
        Assert.Equal(created.Id, listed.Id);
    }

    [Fact]
    public async Task Create_BlankName_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/clients", new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409_CaseInsensitive()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var first = await client.PostAsJsonAsync("/api/clients", new { name = "Acme Corp" });
        first.EnsureSuccessStatusCode();

        var exact = await client.PostAsJsonAsync("/api/clients", new { name = "Acme Corp" });
        Assert.Equal(HttpStatusCode.Conflict, exact.StatusCode);

        var differentCase = await client.PostAsJsonAsync("/api/clients", new { name = "acme corp" });
        Assert.Equal(HttpStatusCode.Conflict, differentCase.StatusCode);
    }

    [Fact]
    public async Task Mutations_AsMember_Succeed()
    {
        // Trust-based domain: members (not just admins) may create/edit/delete clients.
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);

        var created = await (await adminClient.PostAsJsonAsync("/api/clients", new { name = "Acme Corp" }))
            .Content.ReadFromJsonAsync<ClientResponse>();

        var memberToken = await SeedMemberTokenAsync(factory);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var list = await memberClient.GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var create = await memberClient.PostAsJsonAsync("/api/clients", new { name = "Globex" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var memberClientCreated = await create.Content.ReadFromJsonAsync<ClientResponse>();

        var patch = await memberClient.PatchAsJsonAsync($"/api/clients/{created!.Id}", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var delete = await memberClient.DeleteAsync($"/api/clients/{memberClientCreated!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsProjectCounts()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        await SeedClientAsync(factory, "Acme Corp", projectCount: 2);
        await SeedClientAsync(factory, "Globex", projectCount: 0);

        var list = await client.GetFromJsonAsync<List<ClientResponse>>("/api/clients");

        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, list.Single(c => c.Name == "Acme Corp").ProjectCount);
        Assert.Equal(0, list.Single(c => c.Name == "Globex").ProjectCount);
    }

    [Fact]
    public async Task List_StatusFilter_SeparatesActiveAndArchived()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        await SeedClientAsync(factory, "Active Co");
        await SeedClientAsync(factory, "Archived Co", isActive: false);

        var active = await client.GetFromJsonAsync<List<ClientResponse>>("/api/clients");
        Assert.Equal("Active Co", Assert.Single(active!).Name);

        var archived = await client.GetFromJsonAsync<List<ClientResponse>>("/api/clients?status=archived");
        Assert.Equal("Archived Co", Assert.Single(archived!).Name);

        var all = await client.GetFromJsonAsync<List<ClientResponse>>("/api/clients?status=all");
        Assert.Equal(2, all!.Count);

        var invalid = await client.GetAsync("/api/clients?status=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Patch_RenamesAndArchives()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var seeded = await SeedClientAsync(factory, "Acme Corp");

        var rename = await client.PatchAsJsonAsync($"/api/clients/{seeded.Id}", new { name = "Acme Inc" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal("Acme Inc", (await rename.Content.ReadFromJsonAsync<ClientResponse>())!.Name);

        var archive = await client.PatchAsJsonAsync($"/api/clients/{seeded.Id}", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.False((await archive.Content.ReadFromJsonAsync<ClientResponse>())!.IsActive);

        var restore = await client.PatchAsJsonAsync($"/api/clients/{seeded.Id}", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.True((await restore.Content.ReadFromJsonAsync<ClientResponse>())!.IsActive);
    }

    [Fact]
    public async Task Patch_DuplicateName_Returns409_AndUnknownId_Returns404()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        await SeedClientAsync(factory, "Acme Corp");
        var other = await SeedClientAsync(factory, "Globex");

        var conflict = await client.PatchAsJsonAsync($"/api/clients/{other.Id}", new { name = "ACME CORP" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var missing = await client.PatchAsJsonAsync($"/api/clients/{Guid.NewGuid()}", new { name = "Anything" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Delete_WithProjects_Returns409()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var seeded = await SeedClientAsync(factory, "Acme Corp", projectCount: 1);

        var response = await client.DeleteAsync($"/api/clients/{seeded.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutProjects_SoftDeletes_AndListHidesIt()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var seeded = await SeedClientAsync(factory, "Acme Corp");

        var response = await client.DeleteAsync($"/api/clients/{seeded.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var list = await client.GetFromJsonAsync<List<ClientResponse>>("/api/clients?status=all");
        Assert.Empty(list!);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Clients.IgnoreQueryFilters().SingleAsync(c => c.Id == seeded.Id);
        Assert.NotNull(row.DeletedAtUtc);
        Assert.Equal(admin.Id, row.DeletedByUserId);
    }

    private static async Task<Client> SeedClientAsync(
        ReeTrackWebApplicationFactory factory,
        string name,
        bool isActive = true,
        int projectCount = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var client = new Client { Name = name, IsActive = isActive };
        db.Clients.Add(client);

        for (var i = 1; i <= projectCount; i++)
        {
            db.Projects.Add(new Project
            {
                Client = client,
                Name = $"{name} project {i}",
                Status = ProjectStatus.Active,
                BillingType = BillingType.Hourly
            });
        }

        await db.SaveChangesAsync();
        return client;
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

    private sealed class ClientResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public int ProjectCount { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}

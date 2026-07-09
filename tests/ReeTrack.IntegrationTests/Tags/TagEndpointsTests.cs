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

namespace ReeTrack.IntegrationTests.Tags;

/// <summary>
/// Covers /api/tags: CRUD, hex-color normalization, usage counts, the
/// clear-color sentinel, and the deliberately-unguarded delete (tags may be
/// removed even while in use, and their name reused immediately).
/// </summary>
public class TagEndpointsTests
{
    [Fact]
    public async Task Create_NormalizesColorAndTrimsName_AndListIncludesIt()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/tags", new { name = "  Urgent  ", color = "#ff6b4a" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(created);
        Assert.Equal("Urgent", created.Name);
        Assert.Equal("#FF6B4A", created.Color);
        Assert.Equal(0, created.UsageCount);

        var list = await client.GetFromJsonAsync<List<TagResponse>>("/api/tags");
        var listed = Assert.Single(list!);
        Assert.Equal(created.Id, listed.Id);
    }

    [Fact]
    public async Task Create_BlankName_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/tags", new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidColor_Returns400()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/tags", new { name = "Urgent", color = "red" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409_CaseInsensitive()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        (await client.PostAsJsonAsync("/api/tags", new { name = "Urgent" })).EnsureSuccessStatusCode();

        var exact = await client.PostAsJsonAsync("/api/tags", new { name = "Urgent" });
        Assert.Equal(HttpStatusCode.Conflict, exact.StatusCode);

        var differentCase = await client.PostAsJsonAsync("/api/tags", new { name = "urgent" });
        Assert.Equal(HttpStatusCode.Conflict, differentCase.StatusCode);
    }

    [Fact]
    public async Task Mutations_AsMember_Succeed()
    {
        // Trust-based domain: members (not just admins) may create/edit/delete tags.
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);

        var created = await (await adminClient.PostAsJsonAsync("/api/tags", new { name = "Urgent" }))
            .Content.ReadFromJsonAsync<TagResponse>();

        var memberToken = await SeedMemberTokenAsync(factory);
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var list = await memberClient.GetAsync("/api/tags");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var create = await memberClient.PostAsJsonAsync("/api/tags", new { name = "Billable" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var memberTag = await create.Content.ReadFromJsonAsync<TagResponse>();

        var patch = await memberClient.PatchAsJsonAsync($"/api/tags/{created!.Id}", new { color = "#2FBF71" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var delete = await memberClient.DeleteAsync($"/api/tags/{memberTag!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsUsageCounts_AndOrdersByName()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        await SeedTagAsync(factory, "Urgent", usageCount: 2, ownerUserId: admin.Id);
        await SeedTagAsync(factory, "Billable", usageCount: 0, ownerUserId: admin.Id);

        var list = await client.GetFromJsonAsync<List<TagResponse>>("/api/tags");

        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Equal("Billable", list[0].Name); // alphabetical
        Assert.Equal("Urgent", list[1].Name);
        Assert.Equal(2, list.Single(t => t.Name == "Urgent").UsageCount);
        Assert.Equal(0, list.Single(t => t.Name == "Billable").UsageCount);
    }

    [Fact]
    public async Task Patch_RenamesAndUpdatesColor()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var seeded = await SeedTagAsync(factory, "Urgent", ownerUserId: admin.Id);

        var patch = await client.PatchAsJsonAsync($"/api/tags/{seeded.Id}",
            new { name = "Critical", color = "#e0483e" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var updated = await patch.Content.ReadFromJsonAsync<TagResponse>();
        Assert.Equal("Critical", updated!.Name);
        Assert.Equal("#E0483E", updated.Color);
    }

    [Fact]
    public async Task Patch_EmptyColor_ClearsColor_ButOmittedColorKeepsIt()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var seeded = await SeedTagAsync(factory, "Urgent", color: "#FF6B4A", ownerUserId: admin.Id);

        // Renaming without a color field leaves the color untouched.
        var rename = await client.PatchAsJsonAsync($"/api/tags/{seeded.Id}", new { name = "Important" });
        Assert.Equal("#FF6B4A", (await rename.Content.ReadFromJsonAsync<TagResponse>())!.Color);

        // Empty string is the clear sentinel.
        var clear = await client.PatchAsJsonAsync($"/api/tags/{seeded.Id}", new { color = "" });
        Assert.Null((await clear.Content.ReadFromJsonAsync<TagResponse>())!.Color);
    }

    [Fact]
    public async Task Patch_DuplicateName_Returns409_AndUnknownId_Returns404()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        await SeedTagAsync(factory, "Urgent", ownerUserId: admin.Id);
        var other = await SeedTagAsync(factory, "Billable", ownerUserId: admin.Id);

        var conflict = await client.PatchAsJsonAsync($"/api/tags/{other.Id}", new { name = "URGENT" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var missing = await client.PatchAsJsonAsync($"/api/tags/{Guid.NewGuid()}", new { name = "Anything" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Delete_WhileInUse_SoftDeletes_AndKeepsAssociations()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var seeded = await SeedTagAsync(factory, "Urgent", usageCount: 1, ownerUserId: admin.Id);

        var response = await client.DeleteAsync($"/api/tags/{seeded.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var list = await client.GetFromJsonAsync<List<TagResponse>>("/api/tags");
        Assert.Empty(list!);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Tags.IgnoreQueryFilters().SingleAsync(t => t.Id == seeded.Id);
        Assert.NotNull(row.DeletedAtUtc);
        Assert.Equal(admin.Id, row.DeletedByUserId);

        // The historical join row survives the soft delete (the default query
        // filter hides it because the tag is now soft-deleted).
        Assert.True(await db.TimeEntryTags.IgnoreQueryFilters().AnyAsync(t => t.TagId == seeded.Id));
    }

    [Fact]
    public async Task Delete_ThenRecreateSameName_Succeeds()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var seeded = await SeedTagAsync(factory, "Urgent", ownerUserId: admin.Id);

        (await client.DeleteAsync($"/api/tags/{seeded.Id}")).EnsureSuccessStatusCode();

        var recreate = await client.PostAsJsonAsync("/api/tags", new { name = "Urgent" });
        Assert.Equal(HttpStatusCode.OK, recreate.StatusCode);
        Assert.NotEqual(seeded.Id, (await recreate.Content.ReadFromJsonAsync<TagResponse>())!.Id);
    }

    private static async Task<Tag> SeedTagAsync(
        ReeTrackWebApplicationFactory factory,
        string name,
        string? color = null,
        int usageCount = 0,
        Guid ownerUserId = default)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tag = new Tag { Name = name, Color = color };
        db.Tags.Add(tag);

        for (var i = 0; i < usageCount; i++)
        {
            var entry = new TimeEntry
            {
                UserId = ownerUserId,
                Mode = TimeEntryMode.Manual,
                DurationSeconds = 3600
            };
            db.TimeEntries.Add(entry);
            db.TimeEntryTags.Add(new TimeEntryTag { TimeEntry = entry, Tag = tag });
        }

        await db.SaveChangesAsync();
        return tag;
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

    private sealed class TagResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Color { get; init; }
        public int UsageCount { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}

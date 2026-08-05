using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class CustomReportDefinitionServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsSpecJsonWithCamelCasePropertyNames()
    {
        var userId = Guid.NewGuid();
        await using var db = CreateDb();
        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(userId));

        var created = await service.CreateAsync(
            "Camel Spec", description: null, ValidSpec(), CustomReportVisibility.Shared);

        var stored = await db.CustomReportDefinitions.SingleAsync(d => d.Id == created.Id);
        using var doc = JsonDocument.Parse(stored.SpecJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("version", out _));
        Assert.True(root.TryGetProperty("blocks", out var blocks));
        Assert.False(root.TryGetProperty("Version", out _));
        Assert.False(root.TryGetProperty("Blocks", out _));
        Assert.Equal(JsonValueKind.Array, blocks.ValueKind);
        Assert.Equal("kpi", blocks[0].GetProperty("type").GetString());
        Assert.Equal("b1", blocks[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task UpdateAsync_AsNonCreator_ThrowsForbidden_EvenForAnAdmin()
    {
        // EnsureCanEdit previously short-circuited on isAdmin, but the controller is already
        // Admin-only — that made the check unreachable. Ownership is now the only gate.
        var ownerId = Guid.NewGuid();
        var otherAdminId = Guid.NewGuid();
        await using var db = CreateDb();
        var definition = SeedDefinition(db, ownerId, CustomReportVisibility.Shared);
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(otherAdminId));

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateAsync(
                definition.Id, "Blocked", description: null, ValidSpec(), CustomReportVisibility.Shared));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteAsync_AsNonCreator_ThrowsForbidden_EvenForAnAdmin()
    {
        var ownerId = Guid.NewGuid();
        var otherAdminId = Guid.NewGuid();
        await using var db = CreateDb();
        var definition = SeedDefinition(db, ownerId, CustomReportVisibility.Shared);
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(otherAdminId));

        var ex = await Assert.ThrowsAsync<AppException>(() => service.DeleteAsync(definition.Id));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_SomeoneElsesPrivateReport_ThrowsNotFound_NotForbidden()
    {
        // A 403 would confirm the report exists; a private report should look absent instead.
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await using var db = CreateDb();
        var definition = SeedDefinition(db, ownerId, CustomReportVisibility.Private);
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(otherUserId));

        var ex = await Assert.ThrowsAsync<AppException>(() => service.GetByIdAsync(definition.Id));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_OwnPrivateReport_Succeeds()
    {
        var ownerId = Guid.NewGuid();
        await using var db = CreateDb();
        var definition = SeedDefinition(db, ownerId, CustomReportVisibility.Private);
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(ownerId));

        var result = await service.GetByIdAsync(definition.Id);
        Assert.Equal(definition.Id, result.Id);
        Assert.True(result.CanEdit);
    }

    [Fact]
    public async Task GetByIdAsync_SomeoneElsesSharedReport_IsVisibleButNotEditable()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await using var db = CreateDb();
        var definition = SeedDefinition(db, ownerId, CustomReportVisibility.Shared);
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(otherUserId));

        var result = await service.GetByIdAsync(definition.Id);
        Assert.Equal(definition.Id, result.Id);
        Assert.False(result.CanEdit);
    }

    [Fact]
    public async Task ListAsync_HidesOtherUsersPrivateReports()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        await using var db = CreateDb();
        SeedDefinition(db, ownerId, CustomReportVisibility.Private, name: "Owner's Private");
        SeedDefinition(db, ownerId, CustomReportVisibility.Shared, name: "Owner's Shared");
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(viewerId));

        var result = await service.ListAsync();
        Assert.Single(result.Items);
        Assert.Equal("Owner's Shared", result.Items[0].Name);
    }

    [Fact]
    public async Task ListAsync_IncludesTheCallersOwnPrivateReports()
    {
        var ownerId = Guid.NewGuid();
        await using var db = CreateDb();
        SeedDefinition(db, ownerId, CustomReportVisibility.Private, name: "Mine");
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(ownerId));

        var result = await service.ListAsync();
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task ListAsync_MineFilter_ExcludesOthersSharedReports()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await using var db = CreateDb();
        SeedDefinition(db, ownerId, CustomReportVisibility.Shared, name: "Mine");
        SeedDefinition(db, otherId, CustomReportVisibility.Shared, name: "Theirs");
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(ownerId));

        var result = await service.ListAsync(ownerFilter: CustomReportOwnerFilter.Mine);
        Assert.Single(result.Items);
        Assert.Equal("Mine", result.Items[0].Name);
    }

    [Fact]
    public async Task CreateAsync_TwoDifferentUsers_CanUseTheSameName()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await using var db = CreateDb();
        var serviceA = new CustomReportDefinitionService(db, new FakeCurrentUser(userA));
        var serviceB = new CustomReportDefinitionService(db, new FakeCurrentUser(userB));

        await serviceA.CreateAsync("Q3 Margin", null, ValidSpec(), CustomReportVisibility.Private);
        var second = await serviceB.CreateAsync("Q3 Margin", null, ValidSpec(), CustomReportVisibility.Private);

        Assert.Equal("Q3 Margin", second.Name);
    }

    [Fact]
    public async Task CreateAsync_SameUserSameName_Conflicts()
    {
        var userId = Guid.NewGuid();
        await using var db = CreateDb();
        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(userId));

        await service.CreateAsync("Q3 Margin", null, ValidSpec(), CustomReportVisibility.Private);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync("Q3 Margin", null, ValidSpec(), CustomReportVisibility.Private));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task DuplicateAsync_OfSomeoneElsesSharedReport_IsOwnedByTheDuplicator()
    {
        var ownerId = Guid.NewGuid();
        var duplicatorId = Guid.NewGuid();
        await using var db = CreateDb();
        var definition = SeedDefinition(db, ownerId, CustomReportVisibility.Shared, name: "Q3 Margin");
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(duplicatorId));
        var copy = await service.DuplicateAsync(definition.Id);

        Assert.Equal(duplicatorId, copy.CreatedByUserId);
        Assert.Equal("Q3 Margin (copy)", copy.Name);
    }

    [Fact]
    public async Task DuplicateAsync_OfSomeoneElsesPrivateReport_ThrowsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await using var db = CreateDb();
        var definition = SeedDefinition(db, ownerId, CustomReportVisibility.Private);
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(otherUserId));

        var ex = await Assert.ThrowsAsync<AppException>(() => service.DuplicateAsync(definition.Id));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task DuplicateAsync_SkipsTakenCopyNumbers_ScopedToTheDuplicatorOnly()
    {
        // B8: the resolver now does one prefix query instead of probing every candidate name.
        // This pins down that it still finds the first free slot, and that it only looks at the
        // duplicator's own names, not the source owner's.
        var ownerId = Guid.NewGuid();
        var duplicatorId = Guid.NewGuid();
        await using var db = CreateDb();
        var source = SeedDefinition(db, ownerId, CustomReportVisibility.Shared, name: "Report");
        SeedDefinition(db, duplicatorId, CustomReportVisibility.Private, name: "Report (copy)");
        SeedDefinition(db, duplicatorId, CustomReportVisibility.Private, name: "Report (copy 2)");
        // A same-named "(copy)" owned by someone else must not block the duplicator's own slot.
        SeedDefinition(db, ownerId, CustomReportVisibility.Private, name: "Report (copy 3)");
        await db.SaveChangesAsync();

        var service = new CustomReportDefinitionService(db, new FakeCurrentUser(duplicatorId));
        var copy = await service.DuplicateAsync(source.Id);

        Assert.Equal("Report (copy 3)", copy.Name);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"custom-report-definitions-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static CustomReportDefinition SeedDefinition(
        AppDbContext db,
        Guid ownerId,
        CustomReportVisibility visibility,
        string name = "Weekly KPIs")
    {
        var definition = new CustomReportDefinition
        {
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            SpecJson = """{"version":1,"query":{},"blocks":[{"type":"kpi","id":"b1","metrics":["totalHours"]}]}""",
            SchemaVersion = 1,
            CreatedByUserId = ownerId,
            Visibility = visibility,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.CustomReportDefinitions.Add(definition);
        return definition;
    }

    private static CustomReportSpec ValidSpec() =>
        new()
        {
            Blocks = [new KpiBlockSpec { Id = "b1", Metrics = ["totalHours"] }]
        };

    private sealed class FakeCurrentUser(Guid userId, IReadOnlyList<string>? roles = null) : ICurrentUserService
    {
        public Guid UserId => userId;
        public IReadOnlyList<string> Roles { get; } = roles ?? ["Admin"];
        public bool IsAuthenticated => true;
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Auditing;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Auditing;

public class AuditTrailTests : IClassFixture<ReeTrackWebApplicationFactory>
{
    private readonly ReeTrackWebApplicationFactory _factory;

    public AuditTrailTests(ReeTrackWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateInvitation_WritesCreatedAuditRows_WithActorAndRedactedToken()
    {
        var (admin, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/invitations",
            new { email = "audit.target@reetrack.test", roleId = 2 });
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var invitationLog = await db.Set<AuditLog>()
            .Where(l => l.EntityType == nameof(Invitation) && l.Action == AuditAction.Created)
            .OrderByDescending(l => l.OccurredAtUtc)
            .FirstAsync();

        Assert.Equal(admin.Id, invitationLog.ActorUserId);
        Assert.Null(invitationLog.OldValuesJson);
        Assert.NotNull(invitationLog.NewValuesJson);
        Assert.Contains("\"tokenHash\":\"[REDACTED]\"", invitationLog.NewValuesJson);
        Assert.Contains("audit.target@reetrack.test", invitationLog.NewValuesJson);

        var userLog = await db.Set<AuditLog>()
            .Where(l => l.EntityType == nameof(User) && l.Action == AuditAction.Created)
            .OrderByDescending(l => l.OccurredAtUtc)
            .FirstAsync();
        Assert.Equal(admin.Id, userLog.ActorUserId);
    }

    [Fact]
    public async Task SoftDeleteAndRestore_TimeEntry_WritesDeletedAndRestoredRows()
    {
        await _factory.SeedAdminAsync();

        Guid entryId;
        Guid userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = (await db.Users.FirstAsync()).Id;

            var entry = new TimeEntry
            {
                UserId = userId,
                Description = "audit soft delete",
                Mode = TimeEntryMode.Manual,
                DurationSeconds = 600
            };
            db.TimeEntries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;

            entry.DeletedAtUtc = DateTime.UtcNow;
            entry.DeletedByUserId = userId;
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Hidden by the global query filter, still present when ignoring it.
            Assert.False(await db.TimeEntries.AnyAsync(e => e.Id == entryId));
            var deleted = await db.TimeEntries.IgnoreQueryFilters().FirstAsync(e => e.Id == entryId);

            var deleteLog = await db.Set<AuditLog>().FirstAsync(l =>
                l.EntityType == nameof(TimeEntry) &&
                l.EntityId == entryId.ToString() &&
                l.Action == AuditAction.Deleted);
            Assert.Contains("audit soft delete", deleteLog.OldValuesJson);

            deleted.DeletedAtUtc = null;
            deleted.DeletedByUserId = null;
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.TimeEntries.AnyAsync(e => e.Id == entryId));
            Assert.True(await db.Set<AuditLog>().AnyAsync(l =>
                l.EntityId == entryId.ToString() && l.Action == AuditAction.Restored));
        }
    }

    [Fact]
    public async Task ModifyingAuditLog_Throws_AppendOnlyGuard()
    {
        var (admin, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);
        (await client.PostAsJsonAsync(
            "/api/invitations",
            new { email = "guard.check@reetrack.test", roleId = 2 })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var log = await db.Set<AuditLog>().FirstAsync();
        log.EntityType = "Tampered";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditLogsEndpoint_ReturnsPagedRows_ForAdmin_WithFilters()
    {
        var (admin, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);
        (await client.PostAsJsonAsync(
            "/api/invitations",
            new { email = "endpoint.check@reetrack.test", roleId = 2 })).EnsureSuccessStatusCode();

        var result = await client.GetFromJsonAsync<PagedResult<AuditLogDto>>(
            "/api/audit-logs?entityType=Invitation&action=Created&pageSize=10");

        Assert.NotNull(result);
        Assert.True(result!.TotalCount >= 1);
        var row = result.Items[0];
        Assert.Equal("Invitation", row.EntityType);
        Assert.Equal("Created", row.Action);
        Assert.Equal(admin.Email, row.ActorEmail);
        Assert.Contains("[REDACTED]", row.NewValues);

        var badAction = await client.GetAsync("/api/audit-logs?action=Exploded");
        Assert.Equal(HttpStatusCode.BadRequest, badAction.StatusCode);
    }

    [Fact]
    public async Task AuditLogsEndpoint_RejectsNonAdminAndAnonymous()
    {
        await _factory.SeedAdminAsync();

        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/audit-logs")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var member = new User
        {
            Email = "plain.member@reetrack.test",
            Status = UserStatus.Active,
            EmailVerified = true
        };
        db.Users.Add(member);
        await db.SaveChangesAsync();

        var memberToken = jwt.CreateAccessToken(member, ["Member"], out _);
        var memberClient = _factory.CreateAuthenticatedClient(memberToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/audit-logs")).StatusCode);
    }
}

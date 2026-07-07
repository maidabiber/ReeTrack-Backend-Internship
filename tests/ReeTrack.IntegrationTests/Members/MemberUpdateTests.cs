using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Members;

/// <summary>
/// Covers PATCH /api/members/{id}: role changes, activate/deactivate and the
/// last-admin guard. Each test uses its own factory (and therefore its own
/// database) because the guard depends on exactly how many admins exist.
/// </summary>
public class MemberUpdateTests
{
    [Fact]
    public async Task UpdateMember_DeactivatesAndReactivates()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var member = await SeedActiveMemberAsync(factory, "deactivate.me@reetrack.test");

        var deactivate = await client.PatchAsJsonAsync($"/api/members/{member.Id}", new { status = "Disabled" });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await deactivate.Content.ReadFromJsonAsync<MemberResponse>();
        Assert.Equal("Disabled", deactivated!.Status);

        var reactivate = await client.PatchAsJsonAsync($"/api/members/{member.Id}", new { status = "Active" });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        var reactivated = await reactivate.Content.ReadFromJsonAsync<MemberResponse>();
        Assert.Equal("Active", reactivated!.Status);
    }

    [Fact]
    public async Task UpdateMember_ChangesRole()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var member = await SeedActiveMemberAsync(factory, "promote.me@reetrack.test");

        var promote = await client.PatchAsJsonAsync($"/api/members/{member.Id}", new { roleId = 1 });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        var promoted = await promote.Content.ReadFromJsonAsync<MemberResponse>();
        Assert.Equal("Admin", promoted!.Role);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userRole = await db.UserRoles.SingleAsync(ur => ur.UserId == member.Id);
        Assert.Equal((short)1, userRole.RoleId);
    }

    [Fact]
    public async Task UpdateMember_LastActiveAdmin_CannotBeDemoted()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PatchAsJsonAsync($"/api/members/{admin.Id}", new { roleId = 2 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMember_CannotDeactivateSelf()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PatchAsJsonAsync($"/api/members/{admin.Id}", new { status = "Disabled" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMember_AdminDemotion_AllowedWhenAnotherAdminExists()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        await SeedActiveMemberAsync(factory, "second.admin@reetrack.test", roleId: 1);

        var response = await client.PatchAsJsonAsync($"/api/members/{admin.Id}", new { roleId = 2 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var demoted = await response.Content.ReadFromJsonAsync<MemberResponse>();
        Assert.Equal("Member", demoted!.Role);
    }

    [Fact]
    public async Task UpdateMember_InvitedUser_CannotBeDeactivated()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var invite = await client.PostAsJsonAsync("/api/invitations", new
        {
            email = "invited.pending@reetrack.test",
            roleId = 2
        });
        invite.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invited = await db.Users.SingleAsync(u => u.Email == "invited.pending@reetrack.test");

        var response = await client.PatchAsJsonAsync($"/api/members/{invited.Id}", new { status = "Disabled" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMember_RoleChangeForInvitedUser_SyncsPendingInvitation()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var invite = await client.PostAsJsonAsync("/api/invitations", new
        {
            email = "invited.promote@reetrack.test",
            roleId = 2
        });
        invite.EnsureSuccessStatusCode();

        Guid invitedUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            invitedUserId = (await db.Users.SingleAsync(u => u.Email == "invited.promote@reetrack.test")).Id;
        }

        var response = await client.PatchAsJsonAsync($"/api/members/{invitedUserId}", new { roleId = 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invitation = await verifyDb.Invitations
            .SingleAsync(i => i.Email == "invited.promote@reetrack.test" && i.Status == InvitationStatus.Pending);
        Assert.Equal((short)1, invitation.RoleId);
    }

    private static async Task<User> SeedActiveMemberAsync(
        ReeTrackWebApplicationFactory factory,
        string email,
        short roleId = 2)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var user = new User
        {
            Email = email,
            DisplayName = email.Split('@')[0],
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UserRoles = [new UserRole { RoleId = roleId, AssignedAtUtc = now }]
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class MemberResponse
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }
}

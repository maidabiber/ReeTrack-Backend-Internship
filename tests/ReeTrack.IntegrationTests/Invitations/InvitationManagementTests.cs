using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Invitations;

/// <summary>
/// Covers the admin invitation-management endpoints: listing with computed
/// effective status, revoking (including placeholder-user cleanup) and batch
/// invites with per-row results.
/// </summary>
public class InvitationManagementTests : IClassFixture<ReeTrackWebApplicationFactory>
{
    private readonly ReeTrackWebApplicationFactory _factory;

    public InvitationManagementTests(ReeTrackWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListInvitations_RequiresAdmin()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/invitations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListInvitations_ReportsExpiredForPendingPastExpiry()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var email = "expired.listing@reetrack.test";
        var create = await client.PostAsJsonAsync("/api/invitations", new { email, roleId = 2 });
        create.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invitation = await db.Invitations.SingleAsync(i => i.Email == email);
            invitation.ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/invitations");
        response.EnsureSuccessStatusCode();

        var invitations = await response.Content.ReadFromJsonAsync<List<InvitationListItemResponse>>();
        Assert.NotNull(invitations);
        var row = Assert.Single(invitations, i => i.Email == email);
        Assert.Equal("Expired", row.Status);
        Assert.Equal("Test Admin", row.InvitedByName);
    }

    [Fact]
    public async Task RevokeInvitation_RevokesAndRemovesPlaceholderUser()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var email = "revoke.me@reetrack.test";
        var create = await client.PostAsJsonAsync("/api/invitations", new { email, roleId = 2 });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CreateInvitationResponse>();
        Assert.NotNull(created?.Invitation);

        var revoke = await client.PostAsync($"/api/invitations/{created.Invitation.Id}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var body = await revoke.Content.ReadFromJsonAsync<RevokeInvitationResponse>();
        Assert.NotNull(body);
        Assert.Equal("Revoked", body.Invitation.Status);
        Assert.NotNull(body.RemovedUserId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));
        var invitation = await db.Invitations.SingleAsync(i => i.Email == email);
        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
    }

    [Fact]
    public async Task RevokeInvitation_AlreadyRevoked_Conflicts()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var email = "double.revoke@reetrack.test";
        var create = await client.PostAsJsonAsync("/api/invitations", new { email, roleId = 2 });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CreateInvitationResponse>();
        Assert.NotNull(created?.Invitation);

        var first = await client.PostAsync($"/api/invitations/{created.Invitation.Id}/revoke", null);
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsync($"/api/invitations/{created.Invitation.Id}/revoke", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task RevokedInvitation_PreviewStopsResolving()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var email = "revoked.preview@reetrack.test";
        var create = await client.PostAsJsonAsync("/api/invitations", new { email, roleId = 2 });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CreateInvitationResponse>();
        Assert.NotNull(created?.Invitation);

        var inviteUrl = _factory.TransactionalEmail.LastInviteUrl;
        Assert.NotNull(inviteUrl);
        var rawToken = Uri.UnescapeDataString(inviteUrl.Split("token=", StringSplitOptions.None)[1]);

        var revoke = await client.PostAsync($"/api/invitations/{created.Invitation.Id}/revoke", null);
        revoke.EnsureSuccessStatusCode();

        var preview = await _factory.CreateClient()
            .GetAsync($"/api/invitations/preview?token={Uri.EscapeDataString(rawToken)}");
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
    }

    [Fact]
    public async Task BatchInvite_ReturnsPerRowResults()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            db.Users.Add(new Domain.Entities.User
            {
                Email = "batch.active@reetrack.test",
                DisplayName = "Already Active",
                Status = UserStatus.Active,
                EmailVerified = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                UserRoles = [new Domain.Entities.UserRole { RoleId = 2, AssignedAtUtc = now }]
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/invitations/batch", new
        {
            emails = new[]
            {
                "batch.one@reetrack.test",
                "batch.two@reetrack.test",
                "Batch.One@reetrack.test",
                "batch.active@reetrack.test",
                "not-an-email"
            },
            roleId = 2
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BatchInvitationResponse>();
        Assert.NotNull(body);
        Assert.Equal(5, body.Results.Count);

        var batchOneRows = body.Results.Where(r => r.Email == "batch.one@reetrack.test").ToList();
        Assert.Equal(2, batchOneRows.Count);
        Assert.Contains(batchOneRows, r => r.Status == "Invited");
        Assert.Contains(batchOneRows, r => r.Status == "Duplicate");
        Assert.Equal("Invited", Assert.Single(body.Results, r => r.Email == "batch.two@reetrack.test").Status);
        Assert.Equal("AlreadyActive", Assert.Single(body.Results, r => r.Email == "batch.active@reetrack.test").Status);
        Assert.Equal("Invalid", Assert.Single(body.Results, r => r.Email == "not-an-email").Status);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await verifyDb.Invitations.AnyAsync(i => i.Email == "batch.one@reetrack.test"));
        Assert.True(await verifyDb.Invitations.AnyAsync(i => i.Email == "batch.two@reetrack.test"));
        Assert.False(await verifyDb.Invitations.AnyAsync(i => i.Email == "not-an-email"));
    }

    [Fact]
    public async Task BatchInvite_RejectsEmptyList()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/invitations/batch", new
        {
            emails = Array.Empty<string>(),
            roleId = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class CreateInvitationResponse
    {
        public required InvitationResponse Invitation { get; init; }
    }

    private sealed class InvitationResponse
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    private sealed class InvitationListItemResponse
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string InvitedByName { get; init; } = string.Empty;
        public DateTime ExpiresAtUtc { get; init; }
    }

    private sealed class RevokeInvitationResponse
    {
        public required InvitationResponse Invitation { get; init; }
        public Guid? RemovedUserId { get; init; }
    }

    private sealed class BatchInvitationResponse
    {
        public required List<BatchInvitationRowResponse> Results { get; init; }
    }

    private sealed class BatchInvitationRowResponse
    {
        public string Email { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string? Message { get; init; }
    }
}

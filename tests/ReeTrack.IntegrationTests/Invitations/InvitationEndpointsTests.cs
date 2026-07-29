using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Invitations;

public class InvitationEndpointsTests : IClassFixture<ReeTrackWebApplicationFactory>
{
    private readonly ReeTrackWebApplicationFactory _factory;

    public InvitationEndpointsTests(ReeTrackWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateInvitation_RequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/invitations", new
        {
            email = "new.user@reetrack.test",
            roleId = 2
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvitation_CreatesInvitedUserAndSendsEmail()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/invitations", new
        {
            email = "new.user@reetrack.test",
            roleId = 2
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateInvitationResponse>();
        Assert.NotNull(body);
        Assert.Equal("new.user@reetrack.test", body.Member.Email);
        Assert.Equal("Invited", body.Member.Status);
        Assert.Equal("Member", body.Member.Role);
        Assert.NotNull(body.Member.PendingInvitationId);
        Assert.Equal("new.user@reetrack.test", _factory.TransactionalEmail.LastToEmail);
        Assert.Contains("token=", _factory.TransactionalEmail.LastInviteUrl, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invitation = await db.Invitations
            .Where(i => i.Email == "new.user@reetrack.test")
            .SingleAsync();
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public async Task CreateInvitation_RejectsActiveUser()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            db.Users.Add(new Domain.Entities.User
            {
                Email = "active.user@reetrack.test",
                DisplayName = "Active User",
                Status = UserStatus.Active,
                EmailVerified = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                UserRoles =
                [
                    new Domain.Entities.UserRole
                    {
                        RoleId = 2,
                        AssignedAtUtc = now
                    }
                ]
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/invitations", new
        {
            email = "active.user@reetrack.test",
            roleId = 2
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Preview_ReturnsInviteContextForValidToken()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var createResponse = await client.PostAsJsonAsync("/api/invitations", new
        {
            email = "preview.user@reetrack.test",
            roleId = 2
        });
        createResponse.EnsureSuccessStatusCode();

        var inviteUrl = _factory.TransactionalEmail.LastInviteUrl;
        Assert.NotNull(inviteUrl);
        var rawToken = Uri.UnescapeDataString(inviteUrl.Split("token=", StringSplitOptions.None)[1]);

        var previewClient = _factory.CreateClient();
        var previewResponse = await previewClient.GetAsync($"/api/invitations/preview?token={Uri.EscapeDataString(rawToken)}");

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<InvitationPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal("preview.user@reetrack.test", preview.InvitedEmail);
        Assert.Equal("Test Admin", preview.InviterName);
        Assert.Equal("Member", preview.Role);
        Assert.Equal("ReeTrack", preview.AppName);
    }

    [Fact]
    public async Task ListMembers_ReturnsSeededAndInvitedUsers()
    {
        var (admin, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        await client.PostAsJsonAsync("/api/invitations", new
        {
            email = "listed.user@reetrack.test",
            roleId = 2
        });

        var response = await client.GetAsync("/api/members");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<PagedResult<MemberResponse>>();
        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, member => member.Id == admin.Id && member.Status == "Active");
        Assert.Contains(page.Items, member => member.Email == "listed.user@reetrack.test" && member.Status == "Invited");
    }

    private sealed class CreateInvitationResponse
    {
        public required MemberResponse Member { get; init; }
    }

    private sealed class MemberResponse
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public Guid? PendingInvitationId { get; init; }
    }

    private sealed class InvitationPreviewResponse
    {
        public string InvitedEmail { get; init; } = string.Empty;
        public string InviterName { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string AppName { get; init; } = string.Empty;
    }
}

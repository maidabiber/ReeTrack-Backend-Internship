using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Members;

/// <summary>
/// Covers GET /api/members paging, search, and auth boundary.
/// </summary>
public class MemberListEndpointsTests
{
    [Fact]
    public async Task List_PagesAndFiltersByNameOrEmail()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        await SeedActiveMemberAsync(factory, "alpha@reetrack.test", "Alpha User");
        await SeedActiveMemberAsync(factory, "beta@reetrack.test", "Beta User");
        await SeedActiveMemberAsync(factory, "gamma@reetrack.test", "Gamma User");

        // Seeded admin + three members.
        var page1 = await client.GetFromJsonAsync<PagedResult<MemberResponse>>(
            "/api/members?page=1&pageSize=2");
        Assert.Equal(4, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);

        var page2 = await client.GetFromJsonAsync<PagedResult<MemberResponse>>(
            "/api/members?page=2&pageSize=2");
        Assert.Equal(2, page2!.Items.Count);

        var byName = await client.GetFromJsonAsync<PagedResult<MemberResponse>>("/api/members?q=beta");
        Assert.Equal("Beta User", Assert.Single(byName!.Items).DisplayName);

        var byEmail = await client.GetFromJsonAsync<PagedResult<MemberResponse>>("/api/members?q=gamma@");
        Assert.Equal("gamma@reetrack.test", Assert.Single(byEmail!.Items).Email);
    }

    [Fact]
    public async Task List_AsMember_Returns403()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        await factory.SeedAdminAsync();
        var memberToken = await SeedMemberTokenAsync(factory);
        var client = factory.CreateAuthenticatedClient(memberToken);

        var response = await client.GetAsync("/api/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/members");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_AsProjectManager_WithBillableRatesPermission_Succeeds()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, pmToken) = await factory.SeedProjectManagerAsync("pm-list@reetrack.test");
        var pmClient = factory.CreateAuthenticatedClient(pmToken);

        var response = await pmClient.GetAsync("/api/members");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task SeedActiveMemberAsync(
        ReeTrackWebApplicationFactory factory,
        string email,
        string displayName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        db.Users.Add(new User
        {
            Email = email,
            DisplayName = displayName,
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UserRoles = [new UserRole { RoleId = RoleIds.Member, AssignedAtUtc = now }]
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
            Email = "member.list@reetrack.test",
            DisplayName = "List Member",
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

    private sealed class MemberResponse
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        public string Status { get; init; } = string.Empty;
    }
}

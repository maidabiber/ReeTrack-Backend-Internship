using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Members;

/// <summary>
/// Covers GET /api/members pagination and search. Each test gets its own
/// factory so list assertions depend on exactly which users exist.
/// </summary>
public class MemberListEndpointsTests
{
    [Fact]
    public async Task List_PaginationAndSearch_Work()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        await SeedActiveMemberAsync(factory, "alpha@reetrack.test", "Alpha User");
        await SeedActiveMemberAsync(factory, "beta@reetrack.test", "Beta User");
        await SeedActiveMemberAsync(factory, "gamma@reetrack.test", "Gamma User");

        // Seeded admin + 3 members = 4 total
        var page1 = await client.GetFromJsonAsync<PagedResult<MemberResponse>>("/api/members?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(4, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);

        var page2 = await client.GetFromJsonAsync<PagedResult<MemberResponse>>("/api/members?page=2&pageSize=2");
        Assert.Equal(2, page2!.Items.Count);

        var byName = await client.GetFromJsonAsync<PagedResult<MemberResponse>>("/api/members?q=beta");
        Assert.Equal(1, byName!.TotalCount);
        Assert.Equal("Beta User", Assert.Single(byName.Items).DisplayName);

        var byEmail = await client.GetFromJsonAsync<PagedResult<MemberResponse>>("/api/members?q=gamma@");
        Assert.Equal(1, byEmail!.TotalCount);
        Assert.Equal("gamma@reetrack.test", Assert.Single(byEmail.Items).Email);
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

    private sealed class MemberResponse
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        public string Status { get; init; } = string.Empty;
    }
}

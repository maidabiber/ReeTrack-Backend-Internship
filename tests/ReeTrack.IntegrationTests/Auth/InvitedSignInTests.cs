using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Auth;

/// <summary>
/// Covers the first sign-in of an invited user: the pending invitation is
/// accepted (so its link stops resolving) and expired invitations block sign-in.
/// Uses a fake Google exchanger so no real OAuth round-trip is needed.
/// </summary>
public class InvitedSignInTests : IClassFixture<InvitedSignInTests.AuthTestFactory>
{
    private readonly AuthTestFactory _factory;

    public InvitedSignInTests(AuthTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InvitedUser_FirstSignIn_ActivatesAndAcceptsInvitation()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var email = "accepts.invite@reetrack.test";
        var response = await client.PostAsJsonAsync("/api/invitations", new { email, roleId = 2 });
        response.EnsureSuccessStatusCode();

        _factory.Exchanger.Payload = GooglePayload(email);

        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var result = await authService.SignInWithGoogleAsync("fake-code");

        Assert.Equal(email, result.User.Email);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(UserStatus.Active, user.Status);

        var invitation = await db.Invitations.SingleAsync(i => i.Email == email);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(user.Id, invitation.AcceptedByUserId);
        Assert.NotNull(invitation.AcceptedAtUtc);
    }

    [Fact]
    public async Task InvitedUser_ExpiredInvitation_CannotSignIn()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var email = "expired.invite@reetrack.test";
        var response = await client.PostAsJsonAsync("/api/invitations", new { email, roleId = 2 });
        response.EnsureSuccessStatusCode();

        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invitation = await setupDb.Invitations.SingleAsync(i => i.Email == email);
            invitation.ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);
            await setupDb.SaveChangesAsync();
        }

        _factory.Exchanger.Payload = GooglePayload(email);

        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var exception = await Assert.ThrowsAsync<AuthException>(
            () => authService.SignInWithGoogleAsync("fake-code"));
        Assert.Equal(403, exception.StatusCode);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(UserStatus.Invited, user.Status);
    }

    [Fact]
    public async Task InvitedUser_AllInvitationsRevoked_CannotSignIn()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var email = "revoked.invite@reetrack.test";
        var response = await client.PostAsJsonAsync("/api/invitations", new { email, roleId = 2 });
        response.EnsureSuccessStatusCode();

        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invitation = await setupDb.Invitations.SingleAsync(i => i.Email == email);
            invitation.Status = InvitationStatus.Revoked;
            await setupDb.SaveChangesAsync();
        }

        _factory.Exchanger.Payload = GooglePayload(email);

        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var exception = await Assert.ThrowsAsync<AuthException>(
            () => authService.SignInWithGoogleAsync("fake-code"));
        Assert.Equal(403, exception.StatusCode);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(UserStatus.Invited, user.Status);
    }

    private static GoogleTokenPayload GooglePayload(string email) => new()
    {
        Subject = $"google-sub-{email}",
        Email = email,
        EmailVerified = true,
        Name = "Invited Person",
        Picture = null,
    };

    public sealed class AuthTestFactory : ReeTrackWebApplicationFactory
    {
        public FakeGoogleCodeExchanger Exchanger { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IGoogleCodeExchanger)).ToList())
                    services.Remove(descriptor);

                services.AddSingleton<IGoogleCodeExchanger>(Exchanger);
            });
        }
    }

    public sealed class FakeGoogleCodeExchanger : IGoogleCodeExchanger
    {
        public GoogleTokenPayload Payload { get; set; } = new()
        {
            Subject = "google-sub-default",
            Email = "default@reetrack.test",
            EmailVerified = true,
        };

        public Task<GoogleTokenPayload> ExchangeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(Payload);
    }
}

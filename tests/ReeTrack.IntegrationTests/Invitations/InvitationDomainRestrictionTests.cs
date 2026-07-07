using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Invitations;

/// <summary>
/// Covers the configured allowed-domain restriction: invites to domains outside
/// the SSO domain are rejected (per-row for batches) and the allowed domains are
/// exposed to the SPA so it can warn before submitting.
/// </summary>
public class InvitationDomainRestrictionTests : IClassFixture<InvitationDomainRestrictionTests.RestrictedFactory>
{
    private readonly RestrictedFactory _factory;

    public InvitationDomainRestrictionTests(RestrictedFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Invite_RejectsEmailOutsideAllowedDomains()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/invitations",
            new { email = "outsider@gmail.com", roleId = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invite_AllowsEmailOnAllowedDomain()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/invitations",
            new { email = "insider@reeinvent.com", roleId = 2 });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task BatchInvite_MarksDisallowedDomainRowsInvalid()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/invitations/batch", new
        {
            emails = new[] { "ok@reeinvent.com", "nope@gmail.com" },
            roleId = 2
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BatchInvitationResponse>();
        Assert.NotNull(body);
        Assert.Equal("Invited", Assert.Single(body.Results, r => r.Email == "ok@reeinvent.com").Status);
        Assert.Equal("Invalid", Assert.Single(body.Results, r => r.Email == "nope@gmail.com").Status);
    }

    [Fact]
    public async Task AllowedDomains_ReturnsConfiguredDomains()
    {
        var (_, token) = await _factory.SeedAdminAsync();
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/invitations/allowed-domains");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AllowedDomainsResponse>();
        Assert.NotNull(body);
        Assert.Contains("reeinvent.com", body.Domains);
    }

    [Fact]
    public async Task AllowedDomains_RequiresAdmin()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/invitations/allowed-domains");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Factory variant with a single allowed domain configured.</summary>
    public sealed class RestrictedFactory : ReeTrackWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Invitation:AllowedDomains:0"] = "reeinvent.com"
                });
            });
        }
    }

    private sealed class BatchInvitationResponse
    {
        public required List<BatchInvitationRowResponse> Results { get; init; }
    }

    private sealed class BatchInvitationRowResponse
    {
        public string Email { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    private sealed class AllowedDomainsResponse
    {
        public required List<string> Domains { get; init; }
    }
}

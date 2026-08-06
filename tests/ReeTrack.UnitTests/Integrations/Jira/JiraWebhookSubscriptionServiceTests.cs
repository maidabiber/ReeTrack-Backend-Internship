using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Infrastructure.Integrations.Jira;
using ReeTrack.Infrastructure.Persistence;
using Xunit;

namespace ReeTrack.UnitTests.Integrations.Jira;

public class JiraWebhookSubscriptionServiceTests
{
    [Fact]
    public async Task ValidateSignatureAsync_AcceptsValidJiraHmacSignature()
    {
        await using var db = CreateDbContext();
        const string secret = "env-webhook-secret";
        var service = CreateService(db, secret);
        var payload = Encoding.UTF8.GetBytes("""{"webhookEvent":"jira:issue_updated"}""");
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
        var signature = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";

        var isValid = await service.ValidateSignatureAsync(payload, signature);

        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateSignatureAsync_RejectsMissingSecretOrBadSignature()
    {
        await using var db = CreateDbContext();
        var configured = CreateService(db, "env-webhook-secret");
        var unconfigured = CreateService(db, "");
        var payload = Encoding.UTF8.GetBytes("{}");

        Assert.False(await configured.ValidateSignatureAsync(payload, $"sha256={new string('0', 64)}"));
        Assert.False(await unconfigured.ValidateSignatureAsync(payload, "sha256=abcd"));
    }

    [Fact]
    public async Task MarkReceivedAsync_PersistsLastReceivedTimestamp()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, "env-webhook-secret");

        await service.MarkReceivedAsync();

        var settings = Assert.Single(db.JiraWebhookSettings);
        Assert.NotNull(settings.LastReceivedAtUtc);
    }

    [Fact]
    public async Task IsReceiveActiveAsync_DefaultsActiveUnlessExplicitlyDisabled()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, "env-webhook-secret");

        Assert.True(await service.IsReceiveActiveAsync());

        db.JiraWebhookSettings.Add(new ReeTrack.Domain.Entities.JiraWebhookSettings
        {
            Id = Guid.NewGuid(),
            SingletonKey = 1,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        Assert.False(await service.IsReceiveActiveAsync());
    }

    private static JiraWebhookSubscriptionService CreateService(AppDbContext db, string webhookSecret) =>
        new(
            db,
            Options.Create(new JiraOptions
            {
                SiteUrl = "https://example.atlassian.net",
                Email = "user@example.com",
                ApiToken = "token",
                WebhookSecret = webhookSecret
            }));

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}

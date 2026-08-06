using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Jira;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Integrations.Jira;

public sealed class JiraWebhookSubscriptionService : IJiraWebhookSubscriptionService
{
    private const short SettingsKey = 1;

    private readonly IApplicationDbContext _db;
    private readonly JiraOptions _options;

    public JiraWebhookSubscriptionService(
        IApplicationDbContext db,
        IOptions<JiraOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public Task<bool> ValidateSignatureAsync(
        ReadOnlyMemory<byte> payload,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (!_options.IsWebhookConfigured || string.IsNullOrWhiteSpace(signature))
            return Task.FromResult(false);

        const string prefix = "sha256=";
        if (!signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);

        byte[] providedHash;
        try
        {
            providedHash = Convert.FromHexString(signature[prefix.Length..]);
        }
        catch (FormatException)
        {
            return Task.FromResult(false);
        }

        var secret = _options.WebhookSecret.Trim();
        var expectedHash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload.Span);

        return Task.FromResult(
            providedHash.Length == expectedHash.Length
            && CryptographicOperations.FixedTimeEquals(providedHash, expectedHash));
    }

    public async Task<bool> IsReceiveActiveAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.JiraWebhookSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.SingletonKey == SettingsKey, cancellationToken);

        return settings is null || settings.IsActive;
    }

    public async Task MarkReceivedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsWebhookConfigured)
            return;

        var settings = await _db.JiraWebhookSettings
            .SingleOrDefaultAsync(x => x.SingletonKey == SettingsKey, cancellationToken);

        var now = DateTime.UtcNow;
        if (settings is null)
        {
            settings = new JiraWebhookSettings
            {
                Id = Guid.NewGuid(),
                SingletonKey = SettingsKey,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                LastReceivedAtUtc = now
            };
            _db.JiraWebhookSettings.Add(settings);
        }
        else
        {
            settings.LastReceivedAtUtc = now;
            settings.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

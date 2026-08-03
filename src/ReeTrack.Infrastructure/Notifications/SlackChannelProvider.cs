using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Slack;
using ReeTrack.Application.Notifications;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Notifications;

/// <summary>
/// Delivers notifications as Slack DMs by resolving the user's email to a Slack member.
/// </summary>
public sealed class SlackChannelProvider : IChannelProvider
{
    private readonly ISlackApiClient _slack;
    private readonly IApplicationDbContext _db;
    private readonly SlackOptions _options;
    private readonly ILogger<SlackChannelProvider> _logger;

    public SlackChannelProvider(
        ISlackApiClient slack,
        IApplicationDbContext db,
        IOptions<SlackOptions> options,
        ILogger<SlackChannelProvider> logger)
    {
        _slack = slack;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public DeliveryChannel ChannelType => DeliveryChannel.Slack;

    public async Task SendAsync(
        Guid userId,
        NotificationPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogDebug("Skipping Slack notification for user {UserId}: BotToken is not configured.", userId);
            return;
        }

        var toEmail = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning(
                "Skipping Slack notification for user {UserId}: recipient email is missing.",
                userId);
            return;
        }

        var slackUserId = await _slack.LookupUserIdByEmailAsync(toEmail, cancellationToken);
        if (string.IsNullOrWhiteSpace(slackUserId))
        {
            _logger.LogWarning(
                "Skipping Slack notification for user {UserId}: email {Email} is not a Slack workspace member. Invite URL is available on Profile.",
                userId,
                toEmail);
            return;
        }

        var text = BuildMessageText(payload);
        await _slack.SendDirectMessageAsync(slackUserId, text, cancellationToken);
    }

    private static string BuildMessageText(NotificationPayload payload)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(payload.Subject))
            parts.Add($"*{payload.Subject}*");

        if (!string.IsNullOrWhiteSpace(payload.Body))
            parts.Add(payload.Body);

        if (payload.Metadata.TryGetValue(NotificationMetadataKeys.FrontendUrl, out var url)
            && !string.IsNullOrWhiteSpace(url))
        {
            parts.Add(url);
        }

        return string.Join("\n\n", parts);
    }
}

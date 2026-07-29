using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications;

/// <summary>
/// Loads user preferences, filters registered channel providers, and sends concurrently.
/// Workflow types always include InApp; Email defaults on when unset and honors explicit opt-out.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IEnumerable<IChannelProvider> _channelProviders;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IEnumerable<IChannelProvider> channelProviders,
        IApplicationDbContext db,
        ILogger<NotificationDispatcher> logger)
    {
        _channelProviders = channelProviders;
        _db = db;
        _logger = logger;
    }

    public async Task DispatchAsync(
        Guid userId,
        NotificationType notificationType,
        NotificationPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var preferences = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.NotificationType == notificationType)
            .ToListAsync(cancellationToken);

        var enabledChannels = preferences
            .Where(p => p.IsEnabled)
            .Select(p => p.DeliveryChannel)
            .ToHashSet();

        if (NotificationTypeRules.IsInAppMandatory(notificationType))
            enabledChannels.Add(DeliveryChannel.InApp);

        if (NotificationTypeRules.DefaultsEmailWhenUnset(notificationType))
        {
            var hasEmailPreference = preferences.Any(p => p.DeliveryChannel == DeliveryChannel.Email);
            if (!hasEmailPreference)
                enabledChannels.Add(DeliveryChannel.Email);
        }

        if (enabledChannels.Count == 0)
        {
            _logger.LogDebug(
                "No enabled preferences for user {UserId} and notification type {NotificationType}.",
                userId,
                notificationType);
            return;
        }

        var providers = _channelProviders
            .Where(p => enabledChannels.Contains(p.ChannelType))
            .ToList();

        if (providers.Count == 0)
        {
            _logger.LogDebug(
                "No channel providers match enabled preferences for user {UserId} and type {NotificationType}.",
                userId,
                notificationType);
            return;
        }

        await Task.WhenAll(providers.Select(provider =>
            provider.SendAsync(userId, payload, cancellationToken)));
    }
}

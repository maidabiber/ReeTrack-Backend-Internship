using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications;

/// <summary>
/// Loads user preferences, filters registered channel providers, and sends concurrently.
/// Workflow types always include InApp; some channels default on when unset and honor explicit opt-out.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IEnumerable<IChannelProvider> _channelProviders;
    private readonly IApplicationDbContext _db;

    public NotificationDispatcher(
        IEnumerable<IChannelProvider> channelProviders,
        IApplicationDbContext db)
    {
        _channelProviders = channelProviders;
        _db = db;
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

        foreach (DeliveryChannel channel in Enum.GetValues<DeliveryChannel>())
        {
            if (!NotificationTypeRules.IsDefaultEnabledWhenUnset(notificationType, channel))
                continue;

            if (!preferences.Any(p => p.DeliveryChannel == channel))
                enabledChannels.Add(channel);
        }

        if (enabledChannels.Count == 0)
        {
            // Could be logged
            return;
        }

        var providers = _channelProviders
            .Where(p => enabledChannels.Contains(p.ChannelType))
            .ToList();

        if (providers.Count == 0)
        {
            // Could be logged
            return;
        }

        foreach (var provider in providers)
        {
            await provider.SendAsync(userId, payload, cancellationToken);
        }
    }
}

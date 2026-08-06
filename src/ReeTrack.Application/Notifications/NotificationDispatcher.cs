using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications;

/// <summary>
/// Loads user preferences, filters registered channel providers, and sends sequentially.
/// Channels share a scoped DbContext, so they must not run concurrently.
/// Workflow types always include InApp; Email defaults on when unset and honors explicit opt-out.
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

        var hasEmailPreference = preferences.Any(p => p.DeliveryChannel == DeliveryChannel.Email);
        if (!hasEmailPreference && NotificationTypeRules.IsEmailDefaultEnabled(notificationType))
            enabledChannels.Add(DeliveryChannel.Email);

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
            await provider.SendAsync(userId, payload, cancellationToken);
    }
}
using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Notifications;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Notifications;

/// <summary>
/// Persists notifications as in-app inbox items and pushes them over SignalR.
/// </summary>
public sealed class InAppChannelProvider : IChannelProvider
{
    private readonly IApplicationDbContext _db;
    private readonly IInAppNotificationRealtimePublisher _realtimePublisher;

    public InAppChannelProvider(
        IApplicationDbContext db,
        IInAppNotificationRealtimePublisher realtimePublisher)
    {
        _db = db;
        _realtimePublisher = realtimePublisher;
    }

    public DeliveryChannel ChannelType => DeliveryChannel.InApp;

    public async Task SendAsync(
        Guid userId,
        NotificationPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string? actionUrl = null;
        if (payload.Metadata.TryGetValue(NotificationMetadataKeys.FrontendUrl, out var url)
            && !string.IsNullOrWhiteSpace(url))
        {
            actionUrl = url;
        }

        var notification = new InAppNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Subject = payload.Subject,
            Body = payload.Body,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.InAppNotifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _realtimePublisher.PublishCreatedAsync(
                userId,
                notification.Id,
                notification.Subject,
                notification.Body,
                notification.ActionUrl,
                notification.CreatedAtUtc,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Could be logged
        }
    }
}

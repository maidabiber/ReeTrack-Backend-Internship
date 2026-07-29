using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications;

/// <summary>
/// Strategy for delivering a notification over a specific <see cref="DeliveryChannel"/>.
/// </summary>
public interface IChannelProvider
{
    DeliveryChannel ChannelType { get; }

    Task SendAsync(
        Guid userId,
        NotificationPayload payload,
        CancellationToken cancellationToken = default);
}

using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications;

/// <summary>
/// Dispatches a notification to the user's enabled delivery channels.
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(
        Guid userId,
        NotificationType notificationType,
        NotificationPayload payload,
        CancellationToken cancellationToken = default);
}

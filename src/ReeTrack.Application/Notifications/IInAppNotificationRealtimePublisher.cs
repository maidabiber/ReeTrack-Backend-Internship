namespace ReeTrack.Application.Notifications;

/// <summary>
/// Pushes a newly created in-app notification to connected clients in real time.
/// </summary>
public interface IInAppNotificationRealtimePublisher
{
    Task PublishCreatedAsync(
        Guid userId,
        Guid notificationId,
        string subject,
        string body,
        string? actionUrl,
        DateTime createdAtUtc,
        CancellationToken cancellationToken = default);
}

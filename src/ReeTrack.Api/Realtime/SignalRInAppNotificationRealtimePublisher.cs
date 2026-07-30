using Microsoft.AspNetCore.SignalR;
using ReeTrack.Application.Notifications;
using ReeTrack.Api.Hubs;

namespace ReeTrack.Api.Realtime;

/// <summary>
/// SignalR-backed publisher for in-app notification pushes.
/// </summary>
public sealed class SignalRInAppNotificationRealtimePublisher : IInAppNotificationRealtimePublisher
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRInAppNotificationRealtimePublisher(
        IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishCreatedAsync(
        Guid userId,
        Guid notificationId,
        string subject,
        string body,
        string? actionUrl,
        DateTime createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .User(userId.ToString())
                .SendAsync(
                    "ReceiveNotification",
                    new
                    {
                        Id = notificationId,
                        Subject = subject,
                        Body = body,
                        ActionUrl = actionUrl,
                        CreatedAtUtc = createdAtUtc
                    },
                    cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Could be logged
        }
    }
}

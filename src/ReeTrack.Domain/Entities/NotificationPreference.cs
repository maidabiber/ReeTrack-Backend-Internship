using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

/// <summary>
/// Per-user opt-in/out for a specific notification type on a delivery channel.
/// </summary>
public class NotificationPreference : BaseEntity, IAuditable
{
    public Guid UserId { get; set; }
    public NotificationType NotificationType { get; set; }
    public DeliveryChannel DeliveryChannel { get; set; }
    public bool IsEnabled { get; set; }

    public User User { get; set; } = null!;
}

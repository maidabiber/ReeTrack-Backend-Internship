namespace ReeTrack.Api.Contracts;

public sealed class NotificationPreferenceItemRequest
{
    public required string NotificationType { get; init; }
    public required string DeliveryChannel { get; init; }
    public required bool IsEnabled { get; init; }
}

public sealed class UpdateNotificationPreferencesRequest
{
    public required IReadOnlyList<NotificationPreferenceItemRequest> Preferences { get; init; }
}

public sealed class NotificationPreferenceResponse
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string NotificationType { get; init; }
    public required string DeliveryChannel { get; init; }
    public required bool IsEnabled { get; init; }
}

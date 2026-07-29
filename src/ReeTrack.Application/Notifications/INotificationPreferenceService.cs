using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications;

public sealed class NotificationPreferenceDto
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required NotificationType NotificationType { get; init; }
    public required DeliveryChannel DeliveryChannel { get; init; }
    public required bool IsEnabled { get; init; }
}

public sealed class UpsertNotificationPreferenceDto
{
    public required NotificationType NotificationType { get; init; }
    public required DeliveryChannel DeliveryChannel { get; init; }
    public required bool IsEnabled { get; init; }
}

/// <summary>
/// Application API for reading and updating a user's notification preferences.
/// </summary>
public interface INotificationPreferenceService
{
    Task<IReadOnlyList<NotificationPreferenceDto>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationPreferenceDto>> UpsertAsync(
        Guid userId,
        IReadOnlyList<UpsertNotificationPreferenceDto> preferences,
        CancellationToken cancellationToken = default);
}

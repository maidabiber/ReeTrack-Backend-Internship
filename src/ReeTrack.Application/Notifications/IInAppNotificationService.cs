using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Notifications;

public sealed class InAppNotificationDto
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string? ActionUrl { get; init; }
    public required bool IsRead { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Application API for reading and updating the current user's in-app notifications.
/// </summary>
public interface IInAppNotificationService
{
    Task<IReadOnlyList<InAppNotificationDto>> GetUnreadAsync(CancellationToken ct = default);

    Task<PagedResult<InAppNotificationDto>> ListAsync(
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default);
}

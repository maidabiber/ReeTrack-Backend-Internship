using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Notifications;

namespace ReeTrack.Infrastructure.Notifications;

public sealed class InAppNotificationService : IInAppNotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public InAppNotificationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<InAppNotificationDto>> GetUnreadAsync(
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;

        var notifications = await _db.InAppNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);

        return notifications.Select(Map).ToList();
    }

    public async Task<PagedResult<InAppNotificationDto>> ListAsync(
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.InAppNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<InAppNotificationDto>
        {
            Items = items.Select(Map).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;

        var notification = await _db.InAppNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notification is null)
            throw new AppException("Notification was not found.", 404);

        if (notification.IsRead)
            return;

        notification.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    private static InAppNotificationDto Map(Domain.Entities.InAppNotification notification) =>
        new()
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Subject = notification.Subject,
            Body = notification.Body,
            ActionUrl = notification.ActionUrl,
            IsRead = notification.IsRead,
            CreatedAtUtc = notification.CreatedAtUtc
        };
}

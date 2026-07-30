using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Notifications;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IInAppNotificationService _inAppNotifications;

    public NotificationsController(IInAppNotificationService inAppNotifications)
    {
        _inAppNotifications = inAppNotifications;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InAppNotificationDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _inAppNotifications.ListAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("unread")]
    public async Task<ActionResult<IReadOnlyList<InAppNotificationDto>>> GetUnread(
        CancellationToken cancellationToken)
    {
        var notifications = await _inAppNotifications.GetUnreadAsync(cancellationToken);
        return Ok(notifications);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        await _inAppNotifications.MarkAsReadAsync(id, cancellationToken);
        return NoContent();
    }
}

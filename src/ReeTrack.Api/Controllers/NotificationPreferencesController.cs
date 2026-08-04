using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Notifications;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/preferences")]
[Authorize]
public class NotificationPreferencesController : ControllerBase
{
    private readonly INotificationPreferenceService _preferenceService;
    private readonly ICurrentUserService _currentUser;

    public NotificationPreferencesController(
        INotificationPreferenceService preferenceService,
        ICurrentUserService currentUser)
    {
        _preferenceService = preferenceService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationPreferenceResponse>>> Get(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!CanManagePreferences(userId))
            return Forbid();

        var preferences = await _preferenceService.GetByUserAsync(userId, cancellationToken);
        return Ok(preferences.Select(Map).ToList());
    }

    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<NotificationPreferenceResponse>>> Update(
        Guid userId,
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManagePreferences(userId))
            return Forbid();

        if (request.Preferences is null)
            throw AppErrors.Validation("Preferences are required.");

        var upserts = new List<UpsertNotificationPreferenceDto>();
        foreach (var item in request.Preferences)
        {
            if (!Enum.TryParse<NotificationType>(item.NotificationType, ignoreCase: true, out var notificationType))
                throw AppErrors.Validation($"Unknown notification type: {item.NotificationType}.");

            if (!Enum.TryParse<DeliveryChannel>(item.DeliveryChannel, ignoreCase: true, out var deliveryChannel))
                throw AppErrors.Validation($"Unknown delivery channel: {item.DeliveryChannel}.");

            upserts.Add(new UpsertNotificationPreferenceDto
            {
                NotificationType = notificationType,
                DeliveryChannel = deliveryChannel,
                IsEnabled = item.IsEnabled
            });
        }

        var preferences = await _preferenceService.UpsertAsync(userId, upserts, cancellationToken);
        return Ok(preferences.Select(Map).ToList());
    }

    private bool CanManagePreferences(Guid userId) =>
        _currentUser.UserId == userId ||
        _currentUser.Roles.Contains(RoleNames.Admin);

    internal static NotificationPreferenceResponse Map(NotificationPreferenceDto preference) =>
        new()
        {
            Id = preference.Id,
            UserId = preference.UserId,
            NotificationType = preference.NotificationType.ToString(),
            DeliveryChannel = preference.DeliveryChannel.ToString(),
            IsEnabled = preference.IsEnabled
        };
}

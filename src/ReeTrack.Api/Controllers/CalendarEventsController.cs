using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Calendar;
using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Application.Integrations.Calendar.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/calendar")]
public class CalendarEventsController : ControllerBase
{
    private readonly ICalendarIntegrationService _calendarIntegrationService;
    private readonly ICalendarViewService _calendarViewService;
    private readonly IServiceScopeFactory _scopeFactory;

    public CalendarEventsController(
        ICalendarIntegrationService calendarIntegrationService,
        ICalendarViewService calendarViewService,
        IServiceScopeFactory scopeFactory)
    {
        _calendarIntegrationService = calendarIntegrationService;
        _calendarViewService = calendarViewService;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("view")]
    public async Task<ActionResult<CalendarViewResponse>> GetView(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        _ = TriggerStaleSyncAsync(userId);

        var view = await _calendarViewService.GetViewAsync(userId, from, to, cancellationToken);

        return Ok(new CalendarViewResponse
        {
            TimeEntries = view.TimeEntries.Select(TimeEntriesController.MapTimeEntry).ToList(),
            CalendarEvents = view.CalendarEvents
        });
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        _ = TriggerStaleSyncAsync(userId);

        var events = await _calendarIntegrationService.GetEventsAsync(userId, from, to, cancellationToken);
        return Ok(events);
    }

    private async Task TriggerStaleSyncAsync(Guid userId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICalendarIntegrationService>();
            await service.TriggerSyncIfStaleAsync(userId);
        }
        catch
        {
            // Fire-and-forget stale sync.
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(claim, out userId);
    }
}

public sealed class CalendarViewResponse
{
    public required IReadOnlyList<TimeEntryResponse> TimeEntries { get; init; }
    public required IReadOnlyList<SyncedCalendarEventDto> CalendarEvents { get; init; }
}

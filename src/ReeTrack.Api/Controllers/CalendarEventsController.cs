using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Calendar;
using ReeTrack.Application.Common.Interfaces;
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
    private readonly ICurrentUserService _currentUser;
    private readonly IServiceScopeFactory _scopeFactory;

    public CalendarEventsController(
        ICalendarIntegrationService calendarIntegrationService,
        ICalendarViewService calendarViewService,
        ICurrentUserService currentUser,
        IServiceScopeFactory scopeFactory)
    {
        _calendarIntegrationService = calendarIntegrationService;
        _calendarViewService = calendarViewService;
        _currentUser = currentUser;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("view")]
    public async Task<ActionResult<CalendarViewResponse>> GetView(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        _ = TriggerStaleSyncAsync(_currentUser.UserId);

        var view = await _calendarViewService.GetViewAsync(_currentUser.UserId, from, to, cancellationToken);

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
        _ = TriggerStaleSyncAsync(_currentUser.UserId);

        var events = await _calendarIntegrationService.GetEventsAsync(_currentUser.UserId, from, to, cancellationToken);
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

}

public sealed class CalendarViewResponse
{
    public required IReadOnlyList<TimeEntryResponse> TimeEntries { get; init; }
    public required IReadOnlyList<SyncedCalendarEventDto> CalendarEvents { get; init; }
}

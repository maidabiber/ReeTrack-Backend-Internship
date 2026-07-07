using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/time-entries")]
[Authorize]
public class TimeEntriesController : ControllerBase
{
    private readonly ITimeEntryService _timeEntryService;

    public TimeEntriesController(ITimeEntryService timeEntryService)
    {
        _timeEntryService = timeEntryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TimeEntryResponse>>> List(CancellationToken cancellationToken)
    {
        var entries = await _timeEntryService.ListAsync(cancellationToken);
        return Ok(entries.Select(MapTimeEntry).ToList());
    }

    [HttpGet("timer/active")]
    public async Task<ActionResult<TimeEntryResponse>> GetActiveTimer(CancellationToken cancellationToken)
    {
        var entry = await _timeEntryService.GetActiveTimerAsync(cancellationToken);
        if (entry is null)
            return NoContent();

        return Ok(MapTimeEntry(entry));
    }

    [HttpPost("timer/start")]
    public async Task<ActionResult<TimeEntryResponse>> StartTimer(
        [FromBody] StartTimerRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _timeEntryService.StartTimerAsync(
                request?.Description,
                request?.IsBillable ?? true,
                cancellationToken);

            return Ok(MapTimeEntry(entry));
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("timer/stop")]
    public async Task<ActionResult<TimeEntryResponse>> StopTimer(
        [FromBody] StopTimerRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _timeEntryService.StopTimerAsync(request?.Description, cancellationToken);
            return Ok(MapTimeEntry(entry));
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    internal static TimeEntryResponse MapTimeEntry(TimeEntryDto entry) =>
        new()
        {
            Id = entry.Id,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            Mode = entry.Mode,
            StartedAtUtc = entry.StartedAtUtc,
            EndedAtUtc = entry.EndedAtUtc,
            DurationSeconds = entry.DurationSeconds,
            IsRunning = entry.IsRunning
        };
}

public sealed class StartTimerRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
}

public sealed class StopTimerRequest
{
    public string? Description { get; set; }
}

public sealed class TimeEntryResponse
{
    public required Guid Id { get; init; }
    public string? Description { get; init; }
    public required bool IsBillable { get; init; }
    public required string Mode { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
    public required int DurationSeconds { get; init; }
    public required bool IsRunning { get; init; }
}

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
    public async Task<ActionResult> StopTimer(
        [FromBody] StopTimerRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assigneeUserIds = request?.AssigneeUserIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (assigneeUserIds is { Count: > 0 })
            {
                var sharedResult = await _timeEntryService.StopSharedTimerAsync(
                    assigneeUserIds,
                    request?.Description,
                    request?.ConfirmOverlap ?? false,
                    cancellationToken);

                return Ok(new CreateSharedManualEntryResponse
                {
                    Entries = sharedResult.Entries.Select(MapTimeEntry).ToList(),
                    OverlapWarning = sharedResult.OverlapWarning
                });
            }

            var entry = await _timeEntryService.StopTimerAsync(request?.Description, cancellationToken);
            return Ok(MapTimeEntry(entry));
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("manual")]
    public async Task<ActionResult<CreateManualEntryResponse>> CreateManualEntry(
        [FromBody] CreateManualEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _timeEntryService.CreateManualEntryAsync(
                request.Description,
                request.StartedAtUtc,
                request.EndedAtUtc,
                request.IsBillable ?? true,
                request.ConfirmOverlap,
                cancellationToken);

            return Ok(new CreateManualEntryResponse
            {
                Entry = MapTimeEntry(result.Entry),
                OverlapWarning = result.OverlapWarning
            });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("duration")]
    public async Task<ActionResult<CreateManualEntryResponse>> CreateDurationOnlyEntry(
        [FromBody] CreateDurationOnlyEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _timeEntryService.CreateDurationOnlyEntryAsync(
                request.Description,
                request.EntryDateUtc,
                request.DurationSeconds,
                request.IsBillable ?? true,
                cancellationToken);

            return Ok(new CreateManualEntryResponse
            {
                Entry = MapTimeEntry(result.Entry),
                OverlapWarning = result.OverlapWarning
            });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpdateTimeEntryResponse>> UpdateTimeEntry(
        Guid id,
        [FromBody] UpdateTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _timeEntryService.UpdateTimeEntryAsync(
                id,
                request.Description,
                request.StartedAtUtc,
                request.EndedAtUtc,
                request.IsBillable ?? true,
                request.ConfirmOverlap,
                cancellationToken);

            return Ok(new UpdateTimeEntryResponse
            {
                Entry = MapTimeEntry(result.Entry),
                OverlapWarning = result.OverlapWarning
            });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/duration")]
    public async Task<ActionResult<UpdateTimeEntryResponse>> UpdateDurationOnlyEntry(
        Guid id,
        [FromBody] UpdateDurationOnlyEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _timeEntryService.UpdateDurationOnlyEntryAsync(
                id,
                request.Description,
                request.EntryDateUtc,
                request.DurationSeconds,
                request.IsBillable ?? true,
                cancellationToken);

            return Ok(new UpdateTimeEntryResponse
            {
                Entry = MapTimeEntry(result.Entry),
                OverlapWarning = result.OverlapWarning
            });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("shared/manual")]
    public async Task<ActionResult<CreateSharedManualEntryResponse>> CreateSharedManualEntry(
        [FromBody] CreateSharedManualEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assigneeUserIds = ResolveAssigneeUserIds(request);
            var result = await _timeEntryService.CreateSharedManualEntryAsync(
                assigneeUserIds,
                request.Description,
                request.StartedAtUtc,
                request.EndedAtUtc,
                request.IsBillable ?? true,
                request.ConfirmOverlap,
                cancellationToken);

            return Ok(new CreateSharedManualEntryResponse
            {
                Entries = result.Entries.Select(MapTimeEntry).ToList(),
                OverlapWarning = result.OverlapWarning
            });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult<CreateSharedManualEntryResponse>> ShareExistingEntry(
        Guid id,
        [FromBody] ShareExistingEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assigneeUserIds = request.AssigneeUserIds?
                .Where(assigneeId => assigneeId != Guid.Empty)
                .Distinct()
                .ToList() ?? [];

            if (assigneeUserIds.Count == 0)
                throw new AppException("At least one teammate is required.", 400);

            var result = await _timeEntryService.ShareExistingEntryAsync(
                id,
                assigneeUserIds,
                request.ConfirmOverlap,
                cancellationToken);

            return Ok(new CreateSharedManualEntryResponse
            {
                Entries = result.Entries.Select(MapTimeEntry).ToList(),
                OverlapWarning = result.OverlapWarning
            });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    private static IReadOnlyList<Guid> ResolveAssigneeUserIds(CreateSharedManualEntryRequest request)
    {
        if (request.AssigneeUserIds is { Count: > 0 })
            return request.AssigneeUserIds;

        if (request.AssigneeUserId is Guid assigneeUserId)
            return [assigneeUserId];

        throw new AppException("At least one teammate is required.", 400);
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<TimeEntryResponse>>> ListPending(CancellationToken cancellationToken)
    {
        var entries = await _timeEntryService.ListPendingAsync(cancellationToken);
        return Ok(entries.Select(MapTimeEntry).ToList());
    }

    [HttpPut("pending/{id:guid}")]
    public async Task<ActionResult<UpdateTimeEntryResponse>> UpdatePendingEntry(
        Guid id,
        [FromBody] UpdateTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _timeEntryService.UpdatePendingEntryAsync(
                id,
                request.Description,
                request.StartedAtUtc,
                request.EndedAtUtc,
                request.IsBillable ?? true,
                request.ConfirmOverlap,
                cancellationToken);

            return Ok(new UpdateTimeEntryResponse
            {
                Entry = MapTimeEntry(result.Entry),
                OverlapWarning = result.OverlapWarning
            });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("pending/{id:guid}/approve")]
    public async Task<ActionResult<TimeEntryResponse>> ApprovePendingEntry(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _timeEntryService.ApprovePendingEntryAsync(id, cancellationToken);
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
            IsRunning = entry.IsRunning,
            Status = entry.Status,
            SubmittedByUserId = entry.SubmittedByUserId,
            SubmittedByDisplayName = entry.SubmittedByDisplayName,
            AssigneeUserId = entry.AssigneeUserId,
            AssigneeDisplayName = entry.AssigneeDisplayName,
            ShareGroupId = entry.ShareGroupId,
            Participants = entry.Participants
                .Select(participant => new TimeEntryParticipantResponse
                {
                    UserId = participant.UserId,
                    DisplayName = participant.DisplayName,
                    Email = participant.Email,
                    Role = participant.Role
                })
                .ToList()
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
    public List<Guid>? AssigneeUserIds { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class CreateManualEntryRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public required DateTime EndedAtUtc { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class CreateDurationOnlyEntryRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime EntryDateUtc { get; set; }
    public required int DurationSeconds { get; set; }
}

public sealed class CreateSharedManualEntryRequest
{
    public Guid? AssigneeUserId { get; set; }
    public List<Guid>? AssigneeUserIds { get; set; }
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public required DateTime EndedAtUtc { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class ShareExistingEntryRequest
{
    public List<Guid>? AssigneeUserIds { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class CreateSharedManualEntryResponse
{
    public required IReadOnlyList<TimeEntryResponse> Entries { get; init; }
    public string? OverlapWarning { get; init; }
}

public sealed class CreateManualEntryResponse
{
    public required TimeEntryResponse Entry { get; init; }
    public string? OverlapWarning { get; init; }
}

public sealed class UpdateTimeEntryRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public required DateTime EndedAtUtc { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class UpdateDurationOnlyEntryRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime EntryDateUtc { get; set; }
    public required int DurationSeconds { get; set; }
}

public sealed class UpdateTimeEntryResponse
{
    public required TimeEntryResponse Entry { get; init; }
    public string? OverlapWarning { get; init; }
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
    public required string Status { get; init; }
    public Guid? SubmittedByUserId { get; init; }
    public string? SubmittedByDisplayName { get; init; }
    public Guid? AssigneeUserId { get; init; }
    public string? AssigneeDisplayName { get; init; }
    public Guid? ShareGroupId { get; init; }
    public IReadOnlyList<TimeEntryParticipantResponse> Participants { get; init; } = [];
}

public sealed class TimeEntryParticipantResponse
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
}

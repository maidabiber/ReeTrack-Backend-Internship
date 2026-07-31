using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
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
    private readonly ISharedTimeEntryService _sharedTimeEntryService;
    private readonly ISharedTimeEntryApprovalService _sharedTimeEntryApprovalService;

    public TimeEntriesController(
        ITimeEntryService timeEntryService,
        ISharedTimeEntryService sharedTimeEntryService,
        ISharedTimeEntryApprovalService sharedTimeEntryApprovalService)
    {
        _timeEntryService = timeEntryService;
        _sharedTimeEntryService = sharedTimeEntryService;
        _sharedTimeEntryApprovalService = sharedTimeEntryApprovalService;
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
        var input = new StartTimerInput
        {
            Description = request?.Description,
            IsBillable = request?.IsBillable ?? true,
            ProjectId = request?.ProjectId,
            ProjectTaskId = request?.ProjectTaskId,
            TagIds = request?.TagIds
        };
        var entry = await _timeEntryService.StartTimerAsync(input, cancellationToken);

        return Ok(MapTimeEntry(entry));
    }

    [HttpPost("timer/stop")]
    public async Task<ActionResult> StopTimer(
        [FromBody] StopTimerRequest? request,
        CancellationToken cancellationToken)
    {
        var assigneeUserIds = request?.AssigneeUserIds?
            .Where(id => id != Guid.Empty)
            .ToList() ?? [];

        if (assigneeUserIds.Count > 0)
        {
            var sharedInput = new StopSharedTimerInput
            {
                AssigneeUserIds = assigneeUserIds,
                Description = request?.Description,
                IsBillable = request?.IsBillable,
                ProjectId = request?.ProjectId,
                ProjectTaskId = request?.ProjectTaskId,
                TagIds = request?.TagIds
            };
            var sharedResult = await _sharedTimeEntryService.StopSharedTimerAsync(
                sharedInput,
                cancellationToken);

            return Ok(new CreateSharedManualEntryResponse
            {
                Entries = sharedResult.Entries.Select(MapTimeEntry).ToList()
            });
        }

        var stopInput = new StopTimerInput
        {
            Description = request?.Description,
            IsBillable = request?.IsBillable,
            ProjectId = request?.ProjectId,
            ProjectTaskId = request?.ProjectTaskId,
            TagIds = request?.TagIds
        };
        var entry = await _timeEntryService.StopTimerAsync(stopInput, cancellationToken);
        return Ok(MapTimeEntry(entry));
    }

    [HttpPost("manual")]
    public async Task<ActionResult<CreateManualEntryResponse>> CreateManualEntry(
        [FromBody] CreateManualEntryRequest request,
        CancellationToken cancellationToken)
    {
        var input = new CreateManualEntryInput
        {
            Description = request.Description,
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            IsBillable = request.IsBillable ?? true,
            ProjectId = request.ProjectId,
            ProjectTaskId = request.ProjectTaskId,
            TagIds = request.TagIds
        };
        var result = await _timeEntryService.CreateManualEntryAsync(input, cancellationToken);

        return Ok(new CreateManualEntryResponse
        {
            Entry = MapTimeEntry(result.Entry)
        });
    }

    [HttpPost("duration")]
    public async Task<ActionResult<CreateManualEntryResponse>> CreateDurationOnlyEntry(
        [FromBody] CreateDurationOnlyEntryRequest request,
        CancellationToken cancellationToken)
    {
        var input = new CreateDurationOnlyEntryInput
        {
            Description = request.Description,
            EntryDateUtc = request.EntryDateUtc,
            DurationSeconds = request.DurationSeconds,
            IsBillable = request.IsBillable ?? true,
            ProjectId = request.ProjectId,
            ProjectTaskId = request.ProjectTaskId,
            TagIds = request.TagIds
        };
        var result = await _timeEntryService.CreateDurationOnlyEntryAsync(input, cancellationToken);

        return Ok(new CreateManualEntryResponse
        {
            Entry = MapTimeEntry(result.Entry)
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpdateTimeEntryResponse>> UpdateTimeEntry(
        Guid id,
        [FromBody] UpdateTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateTimeEntryInput
        {
            Description = request.Description,
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            IsBillable = request.IsBillable ?? true,
            ProjectId = request.ProjectId,
            ProjectTaskId = request.ProjectTaskId,
            TagIds = request.TagIds
        };
        var result = await _timeEntryService.UpdateTimeEntryAsync(id, input, cancellationToken);

        return Ok(new UpdateTimeEntryResponse
        {
            Entry = MapTimeEntry(result.Entry)
        });
    }

    [HttpPut("{id:guid}/duration")]
    public async Task<ActionResult<UpdateTimeEntryResponse>> UpdateDurationOnlyEntry(
        Guid id,
        [FromBody] UpdateDurationOnlyEntryRequest request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateDurationOnlyEntryInput
        {
            Description = request.Description,
            EntryDateUtc = request.EntryDateUtc,
            DurationSeconds = request.DurationSeconds,
            IsBillable = request.IsBillable ?? true,
            ProjectId = request.ProjectId,
            ProjectTaskId = request.ProjectTaskId,
            TagIds = request.TagIds
        };
        var result = await _timeEntryService.UpdateDurationOnlyEntryAsync(id, input, cancellationToken);

        return Ok(new UpdateTimeEntryResponse
        {
            Entry = MapTimeEntry(result.Entry)
        });
    }

    [HttpPost("shared/manual")]
    public async Task<ActionResult<CreateSharedManualEntryResponse>> CreateSharedManualEntry(
        [FromBody] CreateSharedManualEntryRequest request,
        CancellationToken cancellationToken)
    {
        var input = new CreateSharedManualEntryInput
        {
            AssigneeUserIds = ResolveAssigneeUserIds(request),
            Description = request.Description,
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            IsBillable = request.IsBillable ?? true,
            ProjectId = request.ProjectId,
            ProjectTaskId = request.ProjectTaskId,
            TagIds = request.TagIds
        };
        var result = await _sharedTimeEntryService.CreateSharedManualEntryAsync(input, cancellationToken);

        return Ok(new CreateSharedManualEntryResponse
        {
            Entries = result.Entries.Select(MapTimeEntry).ToList()
        });
    }

    [HttpPost("shared/duration")]
    public async Task<ActionResult<CreateSharedManualEntryResponse>> CreateSharedDurationOnlyEntry(
        [FromBody] CreateSharedDurationOnlyEntryRequest request,
        CancellationToken cancellationToken)
    {
        var assigneeUserIds = request.AssigneeUserIds?
            .Where(id => id != Guid.Empty)
            .ToList() ?? [];

        if (assigneeUserIds.Count == 0 && request.AssigneeUserId is Guid singleId && singleId != Guid.Empty)
            assigneeUserIds = [singleId];

        if (assigneeUserIds.Count == 0)
            throw new AppException("At least one teammate is required.", 400, ErrorCode.TeammatesRequired);

        var input = new CreateSharedDurationOnlyEntryInput
        {
            AssigneeUserIds = assigneeUserIds,
            Description = request.Description,
            EntryDateUtc = request.EntryDateUtc,
            DurationSeconds = request.DurationSeconds,
            IsBillable = request.IsBillable ?? true,
            ProjectId = request.ProjectId,
            ProjectTaskId = request.ProjectTaskId,
            TagIds = request.TagIds
        };
        var result = await _sharedTimeEntryService.CreateSharedDurationOnlyEntryAsync(
            input,
            cancellationToken);

        return Ok(new CreateSharedManualEntryResponse
        {
            Entries = result.Entries.Select(MapTimeEntry).ToList()
        });
    }

    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult<CreateSharedManualEntryResponse>> ShareExistingEntry(
        Guid id,
        [FromBody] ShareExistingEntryRequest request,
        CancellationToken cancellationToken)
    {
        var assigneeUserIds = request.AssigneeUserIds?
            .Where(assigneeId => assigneeId != Guid.Empty)
            .ToList() ?? [];

        if (assigneeUserIds.Count == 0)
            throw new AppException("At least one teammate is required.", 400, ErrorCode.TeammatesRequired);

        var input = new ShareExistingEntryInput
        {
            AssigneeUserIds = assigneeUserIds
        };
        var result = await _sharedTimeEntryService.ShareExistingEntryAsync(
            id,
            input,
            cancellationToken);

        return Ok(new CreateSharedManualEntryResponse
        {
            Entries = result.Entries.Select(MapTimeEntry).ToList()
        });
    }

    private static IReadOnlyList<Guid> ResolveAssigneeUserIds(CreateSharedManualEntryRequest request)
    {
        if (request.AssigneeUserIds is { Count: > 0 })
            return request.AssigneeUserIds;

        if (request.AssigneeUserId is Guid assigneeUserId)
            return [assigneeUserId];

        throw new AppException("At least one teammate is required.", 400, ErrorCode.TeammatesRequired);
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<TimeEntryResponse>>> ListPending(CancellationToken cancellationToken)
    {
        var entries = await _sharedTimeEntryApprovalService.ListPendingAsync(cancellationToken);
        return Ok(entries.Select(MapTimeEntry).ToList());
    }

    [HttpPut("pending/{id:guid}")]
    public async Task<ActionResult<UpdateTimeEntryResponse>> UpdatePendingEntry(
        Guid id,
        [FromBody] UpdateTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var input = new UpdatePendingEntryInput
        {
            Description = request.Description,
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            IsBillable = request.IsBillable ?? true,
            ProjectId = request.ProjectId,
            ProjectTaskId = request.ProjectTaskId,
            TagIds = request.TagIds
        };
        var result = await _sharedTimeEntryApprovalService.UpdatePendingEntryAsync(
            id,
            input,
            cancellationToken);

        return Ok(new UpdateTimeEntryResponse
        {
            Entry = MapTimeEntry(result.Entry)
        });
    }

    [HttpPost("pending/{id:guid}/approve")]
    public async Task<ActionResult<TimeEntryResponse>> ApprovePendingEntry(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entry = await _sharedTimeEntryApprovalService.ApprovePendingEntryAsync(id, cancellationToken);
        return Ok(MapTimeEntry(entry));
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
                .ToList(),
            ProjectId = entry.ProjectId,
            ProjectName = entry.ProjectName,
            ProjectColor = entry.ProjectColor,
            ProjectTaskId = entry.ProjectTaskId,
            ProjectTaskName = entry.ProjectTaskName,
            Tags = entry.Tags
                .Select(tag => new TimeEntryTagResponse
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    Color = tag.Color
                })
                .ToList()
        };
}

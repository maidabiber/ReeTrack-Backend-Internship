using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
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
        [FromBody] TimeEntryRequest? request,
        CancellationToken cancellationToken)
    {
        var input = ToInput(request);
        var entry = await _timeEntryService.CreateAsync(input, cancellationToken);
        return Ok(MapTimeEntry(entry));
    }

    [HttpPost("timer/stop")]
    public async Task<ActionResult<StopTimerResponse>> StopTimer(
        [FromBody] TimeEntryRequest? request,
        CancellationToken cancellationToken)
    {
        var stopInput = ToInput(request);
        var result = await _timeEntryService.StopTimerAsync(stopInput, cancellationToken);
        return Ok(new StopTimerResponse
        {
            Entry = MapTimeEntry(result.Entry),
            HasOverlap = result.HasOverlap,
            OverlapMessage = result.OverlapMessage,
            SuggestedClipEndedAtUtc = result.SuggestedClipEndedAtUtc,
            OverlappingEntries = result.OverlappingEntries
                .Select(item => new OverlapEntryResponse
                {
                    Id = item.Id,
                    Description = item.Description,
                    StartedAtUtc = item.StartedAtUtc,
                    EndedAtUtc = item.EndedAtUtc
                })
                .ToList()
        });
    }

    [HttpPost]
    public async Task<ActionResult<TimeEntryResponse>> Create(
        [FromBody] TimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var input = ToInput(request);
        var entry = await _timeEntryService.CreateAsync(input, cancellationToken);
        return Ok(MapTimeEntry(entry));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TimeEntryResponse>> Update(
        Guid id,
        [FromBody] TimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var input = ToInput(request);
        var entry = await _timeEntryService.UpdateAsync(id, input, cancellationToken);
        return Ok(MapTimeEntry(entry));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _timeEntryService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("shared")]
    public async Task<ActionResult<CreateSharedManualEntryResponse>> CreateShared(
        [FromBody] TimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var assigneeUserIds = ResolveAssigneeUserIds(request);
        var input = ToInput(request);
        var results = await _timeEntryService.CreateAndShareAsync(input, assigneeUserIds, cancellationToken);
        return Ok(new CreateSharedManualEntryResponse
        {
            Entries = results.Select(MapTimeEntry).ToList()
        });
    }

    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult<CreateSharedManualEntryResponse>> ShareExistingEntry(
        Guid id,
        [FromBody] TimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var assigneeUserIds = ResolveAssigneeUserIds(request);
        var entry = await _timeEntryService.ShareEntryAsync(id, assigneeUserIds, cancellationToken);
        return Ok(new CreateSharedManualEntryResponse
        {
            Entries = [MapTimeEntry(entry)]
        });
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<TimeEntryResponse>>> ListPending(CancellationToken cancellationToken)
    {
        var entries = await _timeEntryService.ListPendingEntriesAsync(cancellationToken);
        return Ok(entries.Select(MapTimeEntry).ToList());
    }

    [HttpPut("pending/{id:guid}")]
    public async Task<ActionResult<TimeEntryResponse>> UpdatePending(
        Guid id,
        [FromBody] TimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var input = ToInput(request);
        var entry = await _timeEntryService.UpdateAsync(id, input, cancellationToken);
        return Ok(MapTimeEntry(entry));
    }

    [HttpPost("pending/{id:guid}/approve")]
    public async Task<ActionResult<TimeEntryResponse>> ApprovePending(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entry = await _timeEntryService.ApprovePendingEntryAsync(id, cancellationToken);
        return Ok(MapTimeEntry(entry));
    }

    private static IReadOnlyList<Guid> ResolveAssigneeUserIds(TimeEntryRequest? request)
    {
        if (request?.AssigneeUserIds is { Count: > 0 })
            return request.AssigneeUserIds
                .Where(id => id != Guid.Empty)
                .ToList();

        if (request?.AssigneeUserId is Guid singleId && singleId != Guid.Empty)
            return [singleId];

        return [];
    }

    private static TimeEntryInput ToInput(TimeEntryRequest? request) => new()
    {
        Description = request?.Description,
        IsBillable = request?.IsBillable,
        StartedAtUtc = request?.StartedAtUtc,
        EndedAtUtc = request?.EndedAtUtc,
        EntryDateUtc = request?.EntryDateUtc,
        DurationSeconds = request?.DurationSeconds,
        ProjectId = request?.ProjectId,
        ProjectTaskId = request?.ProjectTaskId,
        TagIds = request?.TagIds
    };

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

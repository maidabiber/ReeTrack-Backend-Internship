using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

    public TimeEntriesController(ITimeEntryService timeEntryService)
    {
        _timeEntryService = timeEntryService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TimeEntryResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? date = null,
        [FromQuery] string sort = "newest",
        [FromQuery] int? utcOffsetMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _timeEntryService.ListAsync(
            page, pageSize, date, sort, utcOffsetMinutes, cancellationToken);
        return Ok(new PagedResult<TimeEntryResponse>
        {
            Items = result.Items.Select(MapTimeEntry).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
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

    /// <summary>
    /// Creates several entries as one unit. Overlapping rows are reported back rather than
    /// thrown as a 409 — the caller drafted a batch and needs to know which rows are the problem.
    /// A 200 with an empty <c>created</c> list and a populated <c>conflicts</c> list means
    /// nothing was written.
    /// </summary>
    [HttpPost("batch")]
    public async Task<ActionResult<CreateTimeEntriesBatchResponse>> CreateBatch(
        [FromBody] CreateTimeEntriesBatchRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Entries is not { Count: > 0 })
            throw AppErrors.Validation("At least one time entry is required.");

        foreach (var item in request.Entries)
            EnsureCompleteTiming(item);

        var inputs = request.Entries.Select(ToInput).ToList();
        var result = await _timeEntryService.CreateBatchAsync(inputs, request.SkipOverlapping, cancellationToken);

        return Ok(new CreateTimeEntriesBatchResponse
        {
            Created = result.Created.Select(MapTimeEntry).ToList(),
            Conflicts = result.Conflicts
                .Select(conflict => new BatchEntryConflictResponse
                {
                    Index = conflict.Index,
                    Message = conflict.Message,
                    OverlappingEntries = conflict.OverlappingEntries
                        .Select(item => new OverlapEntryResponse
                        {
                            Id = item.Id,
                            Description = item.Description,
                            StartedAtUtc = item.StartedAtUtc,
                            EndedAtUtc = item.EndedAtUtc
                        })
                        .ToList(),
                    OverlappingEntryIndexes = conflict.OverlappingEntryIndexes,
                })
                .ToList()
        });
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
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TimeEntryRequest? request,
        CancellationToken cancellationToken)
    {
        var input = request is null ? null : ToInput(request);
        var entry = await _timeEntryService.ApprovePendingEntryAsync(id, input, cancellationToken);
        return Ok(MapTimeEntry(entry));
    }

    [HttpPost("pending/{id:guid}/reject")]
    public async Task<IActionResult> RejectPending(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _timeEntryService.RejectPendingEntryAsync(id, cancellationToken);
        return NoContent();
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

    private static void EnsureCompleteTiming(TimeEntryRequest item)
    {
        var hasRange = item.StartedAtUtc is not null && item.EndedAtUtc is not null;
        var hasDuration = item.EntryDateUtc is not null && item.DurationSeconds is > 0;
        if (!hasRange && !hasDuration)
        {
            throw AppErrors.Validation(
                "Each entry needs either a start/end time range or a date with a duration greater than zero.");
        }
    }

    private static TimeEntryInput ToInput(TimeEntryRequest? request) => new()
    {
        Description = request?.Description,
        IsBillable = request?.IsBillable,
        StartedAtUtc = ToUtc(request?.StartedAtUtc),
        EndedAtUtc = ToUtc(request?.EndedAtUtc),
        EntryDateUtc = ToUtc(request?.EntryDateUtc),
        DurationSeconds = request?.DurationSeconds,
        ProjectId = request?.ProjectId,
        ProjectTaskId = request?.ProjectTaskId,
        TagIds = request?.TagIds,
        UtcOffsetMinutes = request?.UtcOffsetMinutes
    };

    /// <summary>
    /// Npgsql rejects unspecified DateTime for timestamptz; normalize JSON-bound values to UTC.
    /// </summary>
    private static DateTime? ToUtc(DateTime? value)
    {
        if (value is null)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        };
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

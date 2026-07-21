using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/timesheets")]
[Authorize]
public class TimesheetsController : ControllerBase
{
    private readonly ITimesheetService _timesheets;
    private readonly ITimesheetReviewService _review;

    public TimesheetsController(ITimesheetService timesheets, ITimesheetReviewService review)
    {
        _timesheets = timesheets;
        _review = review;
    }

    /// <summary>My week: timesheet status (if any), entries, and submit blockers.</summary>
    [HttpGet("my/week")]
    public async Task<ActionResult<MyWeekTimesheetResponse>> GetMyWeek(
        [FromQuery] DateOnly? weekStart,
        CancellationToken cancellationToken)
    {
        var week = weekStart ?? TimesheetWeek.ToWeekStart(DateTime.UtcNow);
        var result = await _timesheets.GetMyWeekAsync(week, cancellationToken);

        return Ok(new MyWeekTimesheetResponse
        {
            Timesheet = result.Timesheet is null ? null : MapTimesheet(result.Timesheet),
            Entries = result.Entries.Select(MapEntry).ToList(),
            CanSubmit = result.CanSubmit,
            Blockers = result.Blockers
        });
    }

    /// <summary>Per-week totals and statuses for the most recent weeks, newest first.</summary>
    [HttpGet("my/recent")]
    public async Task<ActionResult<IReadOnlyList<WeekSummaryResponse>>> GetRecentWeeks(
        [FromQuery] int count = 8,
        CancellationToken cancellationToken = default)
    {
        var summaries = await _timesheets.GetRecentWeeksAsync(count, cancellationToken);

        return Ok(summaries
            .Select(s => new WeekSummaryResponse
            {
                WeekStartDate = s.WeekStartDate,
                TotalSeconds = s.TotalSeconds,
                BillableSeconds = s.BillableSeconds,
                Status = s.Status,
                TimesheetId = s.TimesheetId
            })
            .ToList());
    }

    [HttpPost("my/submit")]
    public async Task<ActionResult<TimesheetResponse>> Submit(
        [FromBody] SubmitTimesheetRequest request,
        CancellationToken cancellationToken)
    {
        var timesheet = await _timesheets.SubmitAsync(request.WeekStart, cancellationToken);
        return Ok(MapTimesheet(timesheet));
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        await _timesheets.WithdrawAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Admin review queue; status defaults to Submitted, "all" lists every status.</summary>
    [HttpGet("review")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<AdminTimesheetListItemResponse>>> ListForReview(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _review.ListAsync(ParseStatusFilter(status), page, pageSize, cancellationToken);

        return Ok(new PagedResult<AdminTimesheetListItemResponse>
        {
            Items = result.Items.Select(MapListItem).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("review/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminTimesheetDetailResponse>> GetForReview(
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await _review.GetAsync(id, cancellationToken);

        return Ok(new AdminTimesheetDetailResponse
        {
            Timesheet = MapTimesheet(detail.Timesheet),
            UserDisplayName = detail.UserDisplayName,
            UserEmail = detail.UserEmail,
            Entries = detail.Entries.Select(MapEntry).ToList(),
            TotalSeconds = detail.TotalSeconds,
            BillableSeconds = detail.BillableSeconds
        });
    }

    [HttpPost("review/{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TimesheetResponse>> Approve(
        Guid id,
        [FromBody] ReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var timesheet = await _review.ApproveAsync(id, request?.Comment, cancellationToken);
        return Ok(MapTimesheet(timesheet));
    }

    [HttpPost("review/{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TimesheetResponse>> Reject(
        Guid id,
        [FromBody] ReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var timesheet = await _review.RejectAsync(id, request?.Comment, cancellationToken);
        return Ok(MapTimesheet(timesheet));
    }

    private static TimesheetStatus? ParseStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return TimesheetStatus.Submitted;

        if (status.Equals("all", StringComparison.OrdinalIgnoreCase))
            return null;

        if (Enum.TryParse<TimesheetStatus>(status, ignoreCase: true, out var parsed))
            return parsed;

        throw new AppException("Unknown timesheet status filter.", 400);
    }

    private static AdminTimesheetListItemResponse MapListItem(AdminTimesheetListItemDto item) =>
        new()
        {
            Id = item.Id,
            UserId = item.UserId,
            UserDisplayName = item.UserDisplayName,
            UserEmail = item.UserEmail,
            WeekStartDate = item.WeekStartDate,
            Status = item.Status,
            SubmittedAtUtc = item.SubmittedAtUtc,
            TotalSeconds = item.TotalSeconds,
            EntryCount = item.EntryCount
        };

    private static TimesheetResponse MapTimesheet(TimesheetDto timesheet) =>
        new()
        {
            Id = timesheet.Id,
            UserId = timesheet.UserId,
            WeekStartDate = timesheet.WeekStartDate,
            Status = timesheet.Status,
            SubmittedAtUtc = timesheet.SubmittedAtUtc,
            ReviewedByUserId = timesheet.ReviewedByUserId,
            ReviewedByDisplayName = timesheet.ReviewedByDisplayName,
            ReviewedAtUtc = timesheet.ReviewedAtUtc,
            ReviewComment = timesheet.ReviewComment
        };

    private static TimesheetEntryResponse MapEntry(TimesheetEntryDto entry) =>
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
            ProjectName = entry.ProjectName,
            ClientName = entry.ClientName
        };
}

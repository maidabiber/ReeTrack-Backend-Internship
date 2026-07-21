using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/timesheets")]
[Authorize]
public class TimesheetsController : ControllerBase
{
    private readonly ITimesheetService _timesheets;

    public TimesheetsController(ITimesheetService timesheets)
    {
        _timesheets = timesheets;
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

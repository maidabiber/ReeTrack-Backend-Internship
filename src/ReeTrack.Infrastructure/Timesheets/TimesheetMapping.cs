using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Timesheets;

internal static class TimesheetMapping
{
    public static bool IsRunning(TimeEntry entry) =>
        entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null;

    public static TimesheetDto MapTimesheet(Timesheet timesheet) =>
        new()
        {
            Id = timesheet.Id,
            UserId = timesheet.UserId,
            WeekStartDate = timesheet.WeekStartDate,
            Status = timesheet.Status.ToString(),
            SubmittedAtUtc = timesheet.SubmittedAtUtc,
            ReviewedByUserId = timesheet.ReviewedByUserId,
            ReviewedByDisplayName = timesheet.ReviewedByUser?.DisplayName ?? timesheet.ReviewedByUser?.Email,
            ReviewedAtUtc = timesheet.ReviewedAtUtc,
            ReviewComment = timesheet.ReviewComment
        };

    public static TimesheetEntryDto MapEntry(TimeEntry entry) =>
        new()
        {
            Id = entry.Id,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            Mode = entry.Mode.ToString(),
            StartedAtUtc = entry.StartedAtUtc,
            EndedAtUtc = entry.EndedAtUtc,
            DurationSeconds = entry.DurationSeconds,
            IsRunning = IsRunning(entry),
            Status = entry.Status.ToString(),
            ProjectName = entry.Project?.Name,
            ClientName = entry.Project?.Client?.Name
        };
}

namespace ReeTrack.Application.Common.Models;

public sealed class MyWeekTimesheetDto
{
    /// <summary>Null when the week has never been submitted ("draft").</summary>
    public TimesheetDto? Timesheet { get; init; }
    public required IReadOnlyList<TimesheetEntryDto> Entries { get; init; }
    public required bool CanSubmit { get; init; }
    /// <summary>Reasons submit is currently disabled; empty when CanSubmit.</summary>
    public required IReadOnlyList<string> Blockers { get; init; }
}

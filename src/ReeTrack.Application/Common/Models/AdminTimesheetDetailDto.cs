namespace ReeTrack.Application.Common.Models;

public sealed class AdminTimesheetDetailDto
{
    public required TimesheetDto Timesheet { get; init; }
    public string? UserDisplayName { get; init; }
    public required string UserEmail { get; init; }
    public required IReadOnlyList<TimesheetEntryDto> Entries { get; init; }
    public required long TotalSeconds { get; init; }
    public required long BillableSeconds { get; init; }
}

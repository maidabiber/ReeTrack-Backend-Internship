namespace ReeTrack.Application.Common.Models;

public sealed class WeekSummaryDto
{
    public required DateOnly WeekStartDate { get; init; }
    public required long TotalSeconds { get; init; }
    public required long BillableSeconds { get; init; }
    /// <summary>"None" when the week has no timesheet row, otherwise the timesheet status.</summary>
    public required string Status { get; init; }
    public Guid? TimesheetId { get; init; }
}

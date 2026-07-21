namespace ReeTrack.Api.Contracts;

public sealed class SubmitTimesheetRequest
{
    /// <summary>UTC Monday of the week to submit, e.g. "2026-07-13".</summary>
    public required DateOnly WeekStart { get; set; }
}

public sealed class TimesheetResponse
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required DateOnly WeekStartDate { get; init; }
    /// <summary>"Submitted", "Approved" or "Rejected".</summary>
    public required string Status { get; init; }
    public required DateTime SubmittedAtUtc { get; init; }
    public Guid? ReviewedByUserId { get; init; }
    public string? ReviewedByDisplayName { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string? ReviewComment { get; init; }
}

public sealed class TimesheetEntryResponse
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
    public string? ProjectName { get; init; }
    public string? ClientName { get; init; }
}

public sealed class MyWeekTimesheetResponse
{
    public TimesheetResponse? Timesheet { get; init; }
    public required IReadOnlyList<TimesheetEntryResponse> Entries { get; init; }
    public required bool CanSubmit { get; init; }
    public required IReadOnlyList<string> Blockers { get; init; }
}

public sealed class WeekSummaryResponse
{
    public required DateOnly WeekStartDate { get; init; }
    public required long TotalSeconds { get; init; }
    public required long BillableSeconds { get; init; }
    /// <summary>"None" when the week has no timesheet, otherwise the timesheet status.</summary>
    public required string Status { get; init; }
    public Guid? TimesheetId { get; init; }
}

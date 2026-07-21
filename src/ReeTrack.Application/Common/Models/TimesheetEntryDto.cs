namespace ReeTrack.Application.Common.Models;

/// <summary>
/// Slim time-entry shape for timesheet views; unlike TimeEntryDto it carries
/// project/client names (nullable — entries may not be wired to a project yet).
/// </summary>
public sealed class TimesheetEntryDto
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

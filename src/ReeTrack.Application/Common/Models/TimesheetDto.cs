namespace ReeTrack.Application.Common.Models;

public sealed class TimesheetDto
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

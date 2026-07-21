namespace ReeTrack.Application.Common.Models;

public sealed class AdminTimesheetListItemDto
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public string? UserDisplayName { get; init; }
    public required string UserEmail { get; init; }
    public required DateOnly WeekStartDate { get; init; }
    public required string Status { get; init; }
    public required DateTime SubmittedAtUtc { get; init; }
    public required long TotalSeconds { get; init; }
    public required int EntryCount { get; init; }
}

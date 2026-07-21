namespace ReeTrack.Application.Common.Models;

public sealed class TimeEntryDto
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
    public Guid? SubmittedByUserId { get; init; }
    public string? SubmittedByDisplayName { get; init; }
    public Guid? AssigneeUserId { get; init; }
    public string? AssigneeDisplayName { get; init; }
    public Guid? ShareGroupId { get; init; }
    public IReadOnlyList<TimeEntryParticipantDto> Participants { get; init; } = [];

    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public string? ProjectColor { get; init; }
    public Guid? ProjectTaskId { get; init; }
    public string? ProjectTaskName { get; init; }
    public IReadOnlyList<TimeEntryTagDto> Tags { get; init; } = [];
}

namespace ReeTrack.Application.Common.Models;

public sealed class TimeEntryTemplateDto
{
    public required Guid Id { get; init; }
    public required Guid TimeEntryId { get; init; }
    public required Guid? ProjectId { get; init; }
    public required Guid? ProjectTaskId { get; init; }
    public required string? Description { get; init; }
    public required bool IsBillable { get; init; }
    public required TimeOnly? StartTimeUtc { get; init; }
    public required TimeOnly? EndTimeUtc { get; init; }
    public required int DurationSeconds { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

namespace ReeTrack.Application.Common.Models;

public class TimeEntryInput
{
    public string? Description { get; init; }
    public bool? IsBillable { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
    public DateTime? EntryDateUtc { get; init; }
    public int? DurationSeconds { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ProjectTaskId { get; init; }
    public IReadOnlyList<Guid>? TagIds { get; init; }
    public IReadOnlyList<Guid>? AssigneeUserIds { get; init; }
}

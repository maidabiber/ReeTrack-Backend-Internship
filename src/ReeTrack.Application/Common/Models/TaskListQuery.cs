namespace ReeTrack.Application.Common.Models;

public sealed class TaskListQuery
{
    public Guid? ProjectId { get; init; }
    /// <summary>Cross-project list only: when set, tasks belonging to these projects.</summary>
    public IReadOnlyList<Guid>? ProjectIds { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Case-insensitive filter on task name; for cross-project open listing also matches project name.
    /// </summary>
    public string? Q { get; init; }
}

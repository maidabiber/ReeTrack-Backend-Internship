namespace ReeTrack.Application.Common.Models;

public sealed class TaskListQuery
{
    public Guid? ProjectId { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Case-insensitive filter on task name; for cross-project open listing also matches project name.
    /// </summary>
    public string? Q { get; init; }
}

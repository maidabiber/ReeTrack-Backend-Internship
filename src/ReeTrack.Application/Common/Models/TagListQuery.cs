namespace ReeTrack.Application.Common.Models;

public sealed class TagListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>Case-insensitive filter on tag name.</summary>
    public string? Q { get; init; }
}

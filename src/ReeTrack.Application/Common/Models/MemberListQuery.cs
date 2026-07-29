namespace ReeTrack.Application.Common.Models;

public sealed class MemberListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>Case-insensitive filter on display name or email.</summary>
    public string? Q { get; init; }
}

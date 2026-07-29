namespace ReeTrack.Application.Common.Models;

public sealed class ClientListQuery
{
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>Case-insensitive filter on client name.</summary>
    public string? Q { get; init; }
}

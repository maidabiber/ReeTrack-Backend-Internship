namespace ReeTrack.Application.Common.Models;

public sealed class ProjectListQuery
{
    public string? Status { get; init; }
    public Guid? ClientId { get; init; }
    /// <summary>When set, projects whose client is in this set. Combined with ClientId via union of ids.</summary>
    public IReadOnlyList<Guid>? ClientIds { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>Case-insensitive filter on project name or client name.</summary>
    public string? Q { get; init; }

    /// <summary>When true, only return projects created by the current user.</summary>
    public bool? Mine { get; init; }
}

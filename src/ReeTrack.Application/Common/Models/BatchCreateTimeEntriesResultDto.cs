namespace ReeTrack.Application.Common.Models;

/// <summary>
/// A batch row that could not be created because its time range collides with something.
/// </summary>
public sealed class BatchEntryConflictDto
{
    /// <summary>Zero-based position of the offending row in the submitted batch.</summary>
    public required int Index { get; init; }

    public required string Message { get; init; }

    /// <summary>Already-saved entries this row collides with.</summary>
    public IReadOnlyList<OverlapEntryDto> OverlappingEntries { get; init; } = [];

    /// <summary>Other rows of the same batch this row collides with, zero-based.</summary>
    public IReadOnlyList<int> OverlappingEntryIndexes { get; init; } = [];
}

/// <summary>
/// Outcome of a batch create. Conflicts are reported rather than thrown: the caller drafted
/// several entries at once and needs to know which ones landed and which need attention.
/// </summary>
public sealed class BatchCreateTimeEntriesResultDto
{
    public IReadOnlyList<TimeEntryDto> Created { get; init; } = [];

    public IReadOnlyList<BatchEntryConflictDto> Conflicts { get; init; } = [];
}

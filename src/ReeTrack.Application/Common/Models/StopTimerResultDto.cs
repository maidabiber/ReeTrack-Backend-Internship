namespace ReeTrack.Application.Common.Models;

public sealed class OverlapEntryDto
{
    public required Guid Id { get; init; }
    public string? Description { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
}

public sealed class StopTimerResultDto
{
    public required TimeEntryDto Entry { get; init; }
    public required bool HasOverlap { get; init; }
    public string? OverlapMessage { get; init; }
    public DateTime? SuggestedClipEndedAtUtc { get; init; }
    public IReadOnlyList<OverlapEntryDto> OverlappingEntries { get; init; } = [];
}

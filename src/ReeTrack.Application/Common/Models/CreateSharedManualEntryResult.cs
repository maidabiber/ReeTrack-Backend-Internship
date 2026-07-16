namespace ReeTrack.Application.Common.Models;

public sealed class CreateSharedManualEntryResult
{
    public required IReadOnlyList<TimeEntryDto> Entries { get; init; }
}

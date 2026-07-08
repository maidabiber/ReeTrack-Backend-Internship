namespace ReeTrack.Application.Common.Models;

public sealed class CreateManualEntryResult
{
    public required TimeEntryDto Entry { get; init; }
    public string? OverlapWarning { get; init; }
}

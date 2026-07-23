namespace ReeTrack.Api.Contracts;

public sealed class ParseSmartTimeEntryRequest
{
    /// <summary>Free-form text, e.g. "non-billable standup on ReeTrack yesterday 9-10 #meeting".</summary>
    public required string Text { get; set; }
}

public sealed class ParseSmartTimeEntryResponse
{
    public required string Description { get; init; }
    public required int DurationMinutes { get; init; }
    public Guid? MatchedProjectId { get; init; }
    public Guid? MatchedProjectTaskId { get; init; }
    public required IReadOnlyList<Guid> MatchedTagIds { get; init; }
    public required bool IsBillable { get; init; }
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
    public string? EntryDate { get; init; }
    public required double ConfidenceScore { get; init; }
}

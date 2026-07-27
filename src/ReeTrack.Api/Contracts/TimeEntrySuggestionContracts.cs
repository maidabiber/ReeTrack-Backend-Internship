namespace ReeTrack.Api.Contracts;



public sealed class TimeEntrySuggestionResponse

{

    public required Guid? ClientId { get; init; }

    public required Guid? ProjectId { get; init; }

    public required Guid? ProjectTaskId { get; init; }

    public required bool IsBillable { get; init; }

    public required string? SuggestedDescription { get; init; }

    public required TimeOnly? SuggestedStartTimeUtc { get; init; }

    public required TimeOnly? SuggestedEndTimeUtc { get; init; }

    public required int DurationSeconds { get; init; }

    public required double Score { get; init; }

    public required string? ProjectName { get; init; }

    public required string? ProjectColor { get; init; }

    public required string? ProjectTaskName { get; init; }

}



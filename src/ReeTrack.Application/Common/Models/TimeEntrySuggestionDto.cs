namespace ReeTrack.Application.Common.Models;



/// <summary>

/// A suggested time-entry shape derived from the user's recent history.

/// </summary>

public sealed record TimeEntrySuggestionDto(

    Guid? ClientId,

    Guid? ProjectId,

    Guid? ProjectTaskId,

    bool IsBillable,

    string? SuggestedDescription,

    TimeOnly? SuggestedStartTimeUtc,

    TimeOnly? SuggestedEndTimeUtc,

    int DurationSeconds,

    double Score,

    string? ProjectName,

    string? ProjectColor,

    string? ProjectTaskName);



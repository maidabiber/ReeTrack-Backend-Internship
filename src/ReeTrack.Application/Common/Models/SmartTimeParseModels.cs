namespace ReeTrack.Application.Common.Models;

/// <summary>Minimal project identity passed to the LLM for matching.</summary>
public sealed class SmartTimeParseProject
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>Minimal task identity passed to the LLM for matching.</summary>
public sealed class SmartTimeParseTask
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
}

/// <summary>Minimal tag identity passed to the LLM for matching.</summary>
public sealed class SmartTimeParseTag
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>Catalog of entities the LLM may match against.</summary>
public sealed class SmartTimeParseCatalog
{
    public IReadOnlyList<SmartTimeParseProject> Projects { get; init; } = [];
    public IReadOnlyList<SmartTimeParseTask> Tasks { get; init; } = [];
    public IReadOnlyList<SmartTimeParseTag> Tags { get; init; } = [];

    /// <summary>ISO date (YYYY-MM-DD) used to resolve relative phrases like "yesterday".</summary>
    public required DateOnly ReferenceDate { get; init; }
}

/// <summary>Validated result of parsing free-form time-entry text.</summary>
public sealed class ParsedTimeEntryDto
{
    public required string Description { get; init; }
    public required int DurationMinutes { get; init; }
    public Guid? MatchedProjectId { get; init; }
    public Guid? MatchedProjectTaskId { get; init; }
    public IReadOnlyList<Guid> MatchedTagIds { get; init; } = [];
    public required bool IsBillable { get; init; }

    /// <summary>Local start time as HH:mm when a clock time/range was detected; otherwise null.</summary>
    public string? StartTime { get; init; }

    /// <summary>Local end time as HH:mm when a clock time/range was detected; otherwise null.</summary>
    public string? EndTime { get; init; }

    /// <summary>Entry date as YYYY-MM-DD when a date was detected; otherwise null (caller may default to today).</summary>
    public string? EntryDate { get; init; }

    public required double ConfidenceScore { get; init; }
}

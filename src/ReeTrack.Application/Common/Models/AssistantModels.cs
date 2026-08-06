namespace ReeTrack.Application.Common.Models;

public enum AssistantMode { Project = 0, TimeEntry = 1 }

public sealed class AssistantChatRequest
{
    public string? ConversationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<AssistantMessage> History { get; set; } = [];
    public ProjectDraft? CurrentDraft { get; set; }
    public List<MessageMention>? Mentions { get; set; }
    public AssistantMode Mode { get; set; } = AssistantMode.Project;
    public TimeEntryDraft? CurrentTimeEntryDraft { get; set; }

    /// <summary>Client's local yyyy-MM-dd — see the timezone rule in AssistantService.</summary>
    public string? ReferenceDate { get; set; }

    /// <summary>Client IANA timezone (e.g. Europe/Amsterdam).</summary>
    public string? TimeZone { get; set; }

    /// <summary>Client local wall-clock now as yyyy-MM-ddTHH:mm (no Z / offset).</summary>
    public string? ReferenceDateTime { get; set; }
}

/// <summary>
/// An entity the user picked from the UI's @ picker, so the model can use its id without a
/// Search* round trip. For a task mention, <paramref name="ProjectId"/>/<paramref name="ProjectName"/>
/// carry the owning project — without them the drafted row ends up with a task but no project,
/// which the draft form can't render.
/// </summary>
public sealed record MessageMention(
    string Type,
    Guid Id,
    string Name,
    Guid? ProjectId = null,
    string? ProjectName = null);

public sealed class AssistantMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public abstract record AssistantEvent
{
    public sealed record TokenEvent(string Text) : AssistantEvent;
    public sealed record DraftEvent(ProjectDraft Draft) : AssistantEvent;
    public sealed record TimeEntryDraftEvent(TimeEntryDraft Draft) : AssistantEvent;
    public sealed record DoneEvent(string ConversationId, bool DraftCleared = false) : AssistantEvent;
    public sealed record ErrorEvent(string Message) : AssistantEvent;
}

public sealed class TimeEntryDraft
{
    public List<TimeEntryDraftItem> Entries { get; set; } = [];
}

// All wall-clock values are the USER'S LOCAL time, as strings. Never DateTime —
// see the timezone rule in AssistantService for why.
public sealed class TimeEntryDraftItem
{
    public string EntryDate { get; set; } = "";   // yyyy-MM-dd, local
    public string? StartTime { get; set; }         // HH:mm local, null => duration-only
    public string? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ProjectTaskId { get; set; }
    public string? TaskName { get; set; }
    public List<Guid> TagIds { get; set; } = [];
    public List<string> TagNames { get; set; } = [];

    /// <summary>
    /// Null means "not specified by the model this turn" — SubmitTimeEntryDraft overlays it
    /// from the seeded base draft rather than defaulting to true, so an omitted field can't
    /// silently flip an entry the user already marked non-billable back to billable.
    /// </summary>
    public bool? IsBillable { get; set; }
}

public sealed class ProjectDraft
{
    public string Name { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public decimal? HourlyRate { get; set; }
    public decimal? FixedFeeAmount { get; set; }
    public decimal? TimeEstimateHours { get; set; }
    public string? Color { get; set; }
    public List<ProjectTaskDraft> Tasks { get; set; } = [];
}

public sealed class ClientLookupDto(Guid id, string name)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
}

public sealed class ProjectLookupDto(Guid id, string name, string clientName, int taskCount)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string ClientName { get; } = clientName;
    public int TaskCount { get; } = taskCount;
}

public sealed class ProjectTaskDraft
{
    public string Name { get; set; } = string.Empty;
    public decimal? TimeEstimateHours { get; set; }
}

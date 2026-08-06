using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Contracts;

public sealed class AssistantChatRequest
{
    public string? ConversationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<AssistantMessageDto> History { get; set; } = [];
    public ProjectDraftDto? CurrentDraft { get; set; }
    public List<MentionDto>? Mentions { get; set; }

    /// <summary>"project" | "timeEntry". Defaults to project mode when omitted.</summary>
    public string? Mode { get; set; }
    public TimeEntryDraftDto? CurrentTimeEntryDraft { get; set; }

    /// <summary>The client's local yyyy-MM-dd "today", used to resolve relative dates in time-entry mode.</summary>
    public string? ReferenceDate { get; set; }

    /// <summary>Client IANA timezone (e.g. Europe/Amsterdam).</summary>
    public string? TimeZone { get; set; }

    /// <summary>Client local wall-clock now as yyyy-MM-ddTHH:mm (no Z / offset).</summary>
    public string? ReferenceDateTime { get; set; }

    internal AssistantMode ToDomainMode() =>
        Mode?.Equals("timeEntry", StringComparison.OrdinalIgnoreCase) == true
            ? AssistantMode.TimeEntry
            : AssistantMode.Project;
}

public sealed class MentionDto
{
    public string Type { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Owning project — sent for task mentions only, so the draft row gets both.</summary>
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
}

public sealed class AssistantMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class AssistantEventDto
{
    public string Type { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? ToolName { get; set; }
    public string? Arguments { get; set; }
    public string? Result { get; set; }
    public ProjectDraftDto? Draft { get; set; }
    public string? ConversationId { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ProjectDraftDto
{
    public string Name { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public decimal? HourlyRate { get; set; }
    public decimal? FixedFeeAmount { get; set; }
    public decimal? TimeEstimateHours { get; set; }
    public string? Color { get; set; }
    public List<ProjectTaskDraftDto> Tasks { get; set; } = [];

    internal static ProjectDraftDto FromDomain(ProjectDraft draft) => new()
    {
        Name = draft.Name,
        ClientId = draft.ClientId,
        ClientName = draft.ClientName,
        CurrencyCode = draft.CurrencyCode,
        HourlyRate = draft.HourlyRate,
        FixedFeeAmount = draft.FixedFeeAmount,
        TimeEstimateHours = draft.TimeEstimateHours,
        Color = draft.Color,
        Tasks = draft.Tasks.Select(t => new ProjectTaskDraftDto
        {
            Name = t.Name,
            TimeEstimateHours = t.TimeEstimateHours,
        }).ToList(),
    };

    internal ProjectDraft ToDomain() => new()
    {
        Name = Name,
        ClientId = ClientId,
        ClientName = ClientName,
        CurrencyCode = CurrencyCode,
        HourlyRate = HourlyRate,
        FixedFeeAmount = FixedFeeAmount,
        TimeEstimateHours = TimeEstimateHours,
        Color = Color,
        Tasks = Tasks.Select(t => new ReeTrack.Application.Common.Models.ProjectTaskDraft
        {
            Name = t.Name,
            TimeEstimateHours = t.TimeEstimateHours,
        }).ToList(),
    };
}

public sealed class ProjectTaskDraftDto
{
    public string Name { get; set; } = string.Empty;
    public decimal? TimeEstimateHours { get; set; }
}

public sealed class TimeEntryDraftDto
{
    public List<TimeEntryDraftItemDto> Entries { get; set; } = [];

    internal static TimeEntryDraftDto FromDomain(TimeEntryDraft draft) => new()
    {
        Entries = draft.Entries.Select(TimeEntryDraftItemDto.FromDomain).ToList(),
    };

    internal TimeEntryDraft ToDomain() => new()
    {
        Entries = Entries.Select(e => e.ToDomain()).ToList(),
    };
}

// All date/time fields are strings (yyyy-MM-dd / HH:mm), never DateTime — see the
// timezone rule in AssistantService. A DateTime here would let System.Text.Json apply
// its own offset handling on the way in and out.
public sealed class TimeEntryDraftItemDto
{
    public string EntryDate { get; set; } = string.Empty;
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ProjectTaskId { get; set; }
    public string? TaskName { get; set; }
    public List<Guid> TagIds { get; set; } = [];
    public List<string> TagNames { get; set; } = [];
    public bool IsBillable { get; set; } = true;

    internal static TimeEntryDraftItemDto FromDomain(TimeEntryDraftItem item) => new()
    {
        EntryDate = item.EntryDate,
        StartTime = item.StartTime,
        EndTime = item.EndTime,
        DurationMinutes = item.DurationMinutes,
        Description = item.Description,
        ProjectId = item.ProjectId,
        ProjectName = item.ProjectName,
        ProjectTaskId = item.ProjectTaskId,
        TaskName = item.TaskName,
        TagIds = item.TagIds,
        TagNames = item.TagNames,
        IsBillable = item.IsBillable ?? true,
    };

    internal TimeEntryDraftItem ToDomain() => new()
    {
        EntryDate = EntryDate,
        StartTime = StartTime,
        EndTime = EndTime,
        DurationMinutes = DurationMinutes,
        Description = Description,
        ProjectId = ProjectId,
        ProjectName = ProjectName,
        ProjectTaskId = ProjectTaskId,
        TaskName = TaskName,
        TagIds = TagIds,
        TagNames = TagNames,
        IsBillable = IsBillable,
    };
}

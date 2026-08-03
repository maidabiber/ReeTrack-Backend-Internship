namespace ReeTrack.Application.Common.Models;

public sealed class AssistantChatRequest
{
    public string? ConversationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<AssistantMessage> History { get; set; } = [];
    public ProjectDraft? CurrentDraft { get; set; }
    public List<MessageMention>? Mentions { get; set; }
}

public sealed record MessageMention(string Type, Guid Id, string Name);

public sealed class AssistantMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public abstract record AssistantEvent
{
    public sealed record TokenEvent(string Text) : AssistantEvent;
    public sealed record DraftEvent(ProjectDraft Draft) : AssistantEvent;
    public sealed record DoneEvent(string ConversationId, bool DraftCleared = false) : AssistantEvent;
    public sealed record ErrorEvent(string Message) : AssistantEvent;
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

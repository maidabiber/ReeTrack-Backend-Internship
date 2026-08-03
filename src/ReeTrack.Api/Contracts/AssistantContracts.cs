using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Contracts;

public sealed class AssistantChatRequest
{
    public string? ConversationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<AssistantMessageDto> History { get; set; } = [];
    public ProjectDraftDto? CurrentDraft { get; set; }
    public List<MentionDto>? Mentions { get; set; }
}

public sealed class MentionDto
{
    public string Type { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
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

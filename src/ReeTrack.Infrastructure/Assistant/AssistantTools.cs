using Microsoft.Extensions.AI;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using System.ComponentModel;

namespace ReeTrack.Infrastructure.Assistant;

public sealed class AssistantTools
{
    private readonly IClientService _clientService;
    private readonly IProjectService _projectService;
    private ProjectDraft? _baseDraft;

    public AssistantTools(IClientService clientService, IProjectService projectService)
    {
        _clientService = clientService;
        _projectService = projectService;
    }

    public ProjectDraft? CapturedDraft { get; private set; }
    public bool DraftCleared { get; private set; }

    public void Reset()
    {
        CapturedDraft = null;
        DraftCleared = false;
        _baseDraft = null;
    }

    /// <summary>
    /// Seeds the UI draft so SubmitDraft can overlay tool args onto it
    /// instead of wiping fields the model forgot to copy.
    /// </summary>
    public void SeedBaseDraft(ProjectDraft? draft)
    {
        _baseDraft = draft;
    }

    [Description("Search for existing clients by name. Use this to find a client before creating or refining a project draft.")]
    public async Task<string> SearchClients(
        [Description("The search query to match client names against (case-insensitive substring match).")] string query)
    {
        var results = await _clientService.SearchAsync(query, maxResults: 10);
        if (results.Count == 0)
            return "No matching clients found.";

        var lines = results.Select(c => $"- {c.Name} (ID: {c.Id})");
        return string.Join("\n", lines);
    }

    [Description("Search for existing projects by name or client name. Use this to find a project for reference.")]
    public async Task<string> SearchProjects(
        [Description("The search query to match project names or client names against.")] string query)
    {
        var results = await _projectService.SearchAsync(query, maxResults: 10);
        if (results.Count == 0)
            return "No matching projects found.";

        var lines = results.Select(p => $"- {p.Name} (Client: {p.ClientName}, Tasks: {p.TaskCount})");
        return string.Join("\n", lines);
    }

    [Description("Submit a complete project draft for the user to review. Call this when you have gathered enough information and have a complete project specification. When refining, copy unchanged fields from <current_project_draft>.")]
    public string SubmitDraft(
        [Description("The project name. Omit or pass empty to keep the current UI draft name.")] string? name = null,
        [Description("The client ID (GUID) this project belongs to. Use SearchClients to find the correct ID. Pass null to keep the current UI draft client.")] Guid? clientId = null,
        [Description("The client name for display purposes. Pass empty to keep the current UI draft client name.")] string? clientName = null,
        [Description("ISO 4217 currency code, e.g. EUR, USD, GBP. Pass empty to keep the current UI draft currency.")] string? currencyCode = null,
        [Description("Hourly rate for the project. Null keeps the current UI draft value.")] decimal? hourlyRate = null,
        [Description("Fixed fee amount for the project. Null keeps the current UI draft value.")] decimal? fixedFeeAmount = null,
        [Description("Estimated total hours for the project. Null keeps the current UI draft value.")] decimal? timeEstimateHours = null,
        [Description("Hex color code for the project, e.g. #4366E2. Null keeps the current UI draft value.")] string? color = null,
        [Description("List of tasks for the project. Null or empty keeps the current UI draft tasks when a base draft exists.")] List<ProjectTaskDraft>? tasks = null)
    {
        var bas = _baseDraft;

        var resolvedName = !string.IsNullOrWhiteSpace(name) ? name.Trim() : bas?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(resolvedName))
            return "Draft not submitted: project name is required.";

        var resolvedClientId = clientId is not null && clientId != Guid.Empty
            ? clientId
            : bas?.ClientId;

        if (resolvedClientId is null || resolvedClientId == Guid.Empty)
            return "Draft not submitted: a valid clientId is required. Call SearchClients first, then SubmitDraft again with the client ID.";

        var resolvedClientName = !string.IsNullOrWhiteSpace(clientName)
            ? clientName.Trim()
            : bas?.ClientName;

        var resolvedCurrency = !string.IsNullOrWhiteSpace(currencyCode)
            ? currencyCode.Trim().ToUpperInvariant()
            : (!string.IsNullOrWhiteSpace(bas?.CurrencyCode) ? bas!.CurrencyCode : "EUR");

        var resolvedTasks = tasks is { Count: > 0 }
            ? tasks
            : bas?.Tasks ?? [];

        CapturedDraft = new ProjectDraft
        {
            Name = resolvedName,
            ClientId = resolvedClientId,
            ClientName = resolvedClientName,
            CurrencyCode = resolvedCurrency,
            HourlyRate = hourlyRate ?? bas?.HourlyRate,
            FixedFeeAmount = fixedFeeAmount ?? bas?.FixedFeeAmount,
            TimeEstimateHours = timeEstimateHours ?? bas?.TimeEstimateHours,
            Color = color ?? bas?.Color,
            Tasks = resolvedTasks
        };

        return $"Project draft submitted: \"{CapturedDraft.Name}\" for client \"{CapturedDraft.ClientName}\" with {CapturedDraft.Tasks.Count} task(s).";
    }

    [Description("Clear the current project draft. Call this when the user's message is not related to project creation or refinement, or when they explicitly want to discard the draft.")]
    public string ClearDraft()
    {
        CapturedDraft = null;
        DraftCleared = true;
        return "Current draft has been cleared.";
    }

    public IList<AITool> ToToolList()
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(SearchClients),
            AIFunctionFactory.Create(SearchProjects),
            AIFunctionFactory.Create(SubmitDraft),
            AIFunctionFactory.Create(ClearDraft)
        };
    }
}

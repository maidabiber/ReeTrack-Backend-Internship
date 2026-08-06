using System.Globalization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Common;
using System.ComponentModel;

namespace ReeTrack.Infrastructure.Assistant;

public sealed class TimeEntryAssistantTools
{
    private readonly IProjectService _projectService;
    private readonly IProjectTaskService _projectTaskService;
    private readonly ITagService _tagService;
    private readonly ILogger<TimeEntryAssistantTools> _logger;

    // Hallucinated-id defence: only ids we have actually seen (via search results,
    // the seeded base draft, or an explicit UI mention) are trusted in SubmitTimeEntryDraft.
    private readonly Dictionary<Guid, string> _knownProjects = new();
    private readonly Dictionary<Guid, KnownTask> _knownTasks = new();
    private readonly Dictionary<Guid, string> _knownTags = new();

    private readonly record struct KnownTask(string Name, Guid? ProjectId);

    /// <summary>How far a drafted entryDate may sit from the user's local today. See SubmitTimeEntryDraft.</summary>
    private const int MaxDaysFromReferenceDate = 366;

    // Seeded draft, kept around (not just folded into the known-id maps) so
    // SubmitTimeEntryDraft can overlay fields the model omits this turn — see there.
    private TimeEntryDraft? _baseDraft;
    private DateOnly _referenceDate = DateOnly.FromDateTime(DateTime.Today);

    public TimeEntryAssistantTools(
        IProjectService projectService,
        IProjectTaskService projectTaskService,
        ITagService tagService,
        ILogger<TimeEntryAssistantTools> logger)
    {
        _projectService = projectService;
        _projectTaskService = projectTaskService;
        _tagService = tagService;
        _logger = logger;
    }

    public TimeEntryDraft? CapturedDraft { get; private set; }
    public bool DraftCleared { get; private set; }

    public void Reset()
    {
        CapturedDraft = null;
        DraftCleared = false;
        _baseDraft = null;
        _referenceDate = DateOnly.FromDateTime(DateTime.Today);
        _knownProjects.Clear();
        _knownTasks.Clear();
        _knownTags.Clear();
    }

    /// <summary>Sets the client's local "today" used by expandWeek date resolution.</summary>
    public void SetReferenceDate(DateOnly referenceDate) => _referenceDate = referenceDate;

    /// <summary>
    /// Seeds the UI draft so SubmitTimeEntryDraft's hallucinated-id defence trusts ids
    /// already present in the current draft, and so a row's description, billable flag,
    /// and project/task/tags survive when the model omits them for an unrelated edit.
    /// </summary>
    public void SeedBaseDraft(TimeEntryDraft? draft)
    {
        _baseDraft = draft;

        foreach (var entry in draft?.Entries ?? [])
        {
            if (entry.ProjectId is Guid projectId && !string.IsNullOrWhiteSpace(entry.ProjectName))
                _knownProjects[projectId] = entry.ProjectName!;

            if (entry.ProjectTaskId is Guid taskId && !string.IsNullOrWhiteSpace(entry.TaskName))
                _knownTasks[taskId] = new KnownTask(entry.TaskName!, entry.ProjectId);

            for (var i = 0; i < entry.TagIds.Count && i < entry.TagNames.Count; i++)
                _knownTags[entry.TagIds[i]] = entry.TagNames[i];
        }
    }

    /// <summary>
    /// Registers an id the user explicitly picked via the UI's @ mention picker as trusted,
    /// without requiring a Search* round trip.
    /// </summary>
    /// <remarks>
    /// A task mention carries its owning project, which matters: with a null project id the
    /// back-fill in <see cref="SubmitTimeEntryDraft"/> can't run, and the row lands with a task
    /// but no project — which the draft form renders as an empty, disabled task field.
    /// </remarks>
    public void RegisterMention(string type, Guid id, string name, Guid? projectId = null, string? projectName = null)
    {
        switch (type.ToLowerInvariant())
        {
            case "project":
                _knownProjects[id] = name;
                break;
            case "task":
                _knownTasks[id] = new KnownTask(name, NonEmpty(projectId));
                if (NonEmpty(projectId) is Guid taskProjectId && !string.IsNullOrWhiteSpace(projectName))
                    _knownProjects[taskProjectId] = projectName;
                break;
            case "tag":
                _knownTags[id] = name;
                break;
        }
    }

    [Description("Search for existing projects by name or client name. Use this to find a project before drafting time entries.")]
    public async Task<string> SearchProjects(
        [Description("The search query to match project names or client names against.")] string query)
    {
        var results = await _projectService.SearchAsync(query, maxResults: 10);
        if (results.Count == 0)
            return "No matching projects found.";

        foreach (var p in results)
            _knownProjects[p.Id] = p.Name;

        var lines = results.Select(p => $"- {p.Name} (ID: {p.Id}, Client: {p.ClientName})");
        return string.Join("\n", lines);
    }

    [Description("Search for existing open tasks by name or project name. Use this to find a task to log time against.")]
    public async Task<string> SearchTasks(
        [Description("The search query to match task names or project names against.")] string query)
    {
        var result = await _projectTaskService.ListAcrossProjectsAsync(new TaskListQuery
        {
            Q = query,
            Status = "open",
            PageSize = 10
        });

        if (result.Items.Count == 0)
            return "No matching open tasks found.";

        foreach (var t in result.Items)
        {
            _knownTasks[t.Id] = new KnownTask(t.Name, t.ProjectId);
            if (!string.IsNullOrWhiteSpace(t.ProjectName))
                _knownProjects[t.ProjectId] = t.ProjectName;
        }

        var lines = result.Items.Select(t => $"- {t.Name} (ID: {t.Id}, Project: {t.ProjectName}, ProjectId: {t.ProjectId})");
        return string.Join("\n", lines);
    }

    [Description("Search for existing tags by name. Use this to find a tag to attach to time entries.")]
    public async Task<string> SearchTags(
        [Description("The search query to match tag names against.")] string query)
    {
        var result = await _tagService.ListAsync(new TagListQuery
        {
            Q = query,
            PageSize = 10
        });

        if (result.Items.Count == 0)
            return "No matching tags found.";

        foreach (var t in result.Items)
            _knownTags[t.Id] = t.Name;

        var lines = result.Items.Select(t => $"- {t.Name} (ID: {t.Id})");
        return string.Join("\n", lines);
    }

    [Description("Submit a recurring week of identical time-entry drafts for user review. Use this for \"every day next/this/last week\" (and similar). The server fills Monday-based entryDate values — do not invent dates. Does NOT save entries; the user confirms Create in the UI.")]
    public string SubmitWeekTimeEntryDraft(
        [Description("Which week relative to today: \"this\", \"next\", or \"last\".")]
        string expandWeek,
        [Description("Which days: \"weekdays\" (Mon–Fri, default), \"all\" (Mon–Sun), or comma-separated names like \"monday,wednesday,friday\".")]
        string? expandDays = "weekdays",
        [Description("Local start time HH:mm when the user gave a clock range; omit with endTime for duration-only.")]
        string? startTime = null,
        [Description("Local end time HH:mm when the user gave a clock range; omit with startTime for duration-only.")]
        string? endTime = null,
        [Description("Duration in minutes when the user gave an amount of time without a clock range.")]
        int durationMinutes = 0,
        [Description("Optional description shared by every expanded entry.")]
        string? description = null,
        [Description("Resolved project GUID from SearchProjects or a UI mention.")]
        Guid? projectId = null,
        [Description("Resolved task GUID from SearchTasks or a UI mention.")]
        Guid? projectTaskId = null,
        [Description("Resolved tag GUIDs from SearchTags or UI mentions.")]
        List<Guid>? tagIds = null,
        [Description("Whether entries are billable. Defaults to true when omitted.")]
        bool? isBillable = true)
    {
        var dates = AssistantDateContext.ResolveWeekDates(_referenceDate, expandWeek, expandDays ?? "weekdays");
        if (dates is null || dates.Count == 0)
        {
            return "Draft not submitted: expandWeek must be this|next|last and expandDays must be weekdays, all, " +
                   "or a comma-separated weekday list (e.g. monday,friday).";
        }

        var entries = dates.Select(date => new TimeEntryDraftItem
        {
            EntryDate = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            StartTime = startTime,
            EndTime = endTime,
            DurationMinutes = durationMinutes,
            Description = description,
            ProjectId = projectId,
            ProjectTaskId = projectTaskId,
            TagIds = tagIds ?? [],
            IsBillable = isBillable,
        }).ToList();

        return SubmitTimeEntryDraft(entries);
    }

    [Description("Submit the complete list of time-entry drafts for the user to review. Call this for specific dates (not a whole named week). Include ALL entries, not just the ones that changed since <current_time_entry_draft>. For an entry that already exists, you only need to include the fields that changed — description, isBillable, projectId, projectTaskId, and tagIds all fall back to the matching row (by position) in <current_time_entry_draft> when omitted or empty. Does NOT save entries; the user confirms Create in the UI.")]
    public string SubmitTimeEntryDraft(
        [Description("One row per calendar entry. EntryDate is yyyy-MM-dd local. StartTime/EndTime are HH:mm local and 24-hour; omit both when the user only gave an amount of time (duration-only). Never include a UTC offset or 'Z'.")]
        List<TimeEntryDraftItem> entries)
    {
        if (entries is not { Count: > 0 })
            return "Draft not submitted: at least one entry is required.";

        var resolved = new List<TimeEntryDraftItem>(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            var item = entries[i];
            var rowLabel = $"Entry {i + 1}";
            var baseEntry = _baseDraft?.Entries is { } baseEntries && i < baseEntries.Count ? baseEntries[i] : null;

            var entryDate = LlmValueParser.NormalizeDate(item.EntryDate);
            if (entryDate is null)
                return $"Draft not submitted: {rowLabel} has an invalid entryDate. Use yyyy-MM-dd.";

            // Sanity window around the user's real "today". NormalizeDate only proves the string
            // is a real calendar date — it happily accepts a year the model carried over from its
            // training data, which is the failure the prompt's calendar block exists to prevent.
            var parsedDate = DateOnly.ParseExact(entryDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (Math.Abs(parsedDate.DayNumber - _referenceDate.DayNumber) > MaxDaysFromReferenceDate)
            {
                return $"Draft not submitted: {rowLabel} has entryDate {entryDate}, which is more than a year " +
                       $"from today ({_referenceDate:yyyy-MM-dd}). Re-read the calendar in the system prompt and use a date from it.";
            }

            string? startTime = null;
            string? endTime = null;

            if (!string.IsNullOrWhiteSpace(item.StartTime) || !string.IsNullOrWhiteSpace(item.EndTime))
            {
                startTime = LlmValueParser.NormalizeTime(item.StartTime);
                endTime = LlmValueParser.NormalizeTime(item.EndTime);

                if (startTime is null || endTime is null)
                    return $"Draft not submitted: {rowLabel} has an invalid startTime/endTime. Use 24-hour HH:mm, or omit both for a duration-only entry.";
            }

            var durationMinutes = item.DurationMinutes;
            if (durationMinutes <= 0 && startTime is not null && endTime is not null
                && TimeOnly.TryParse(startTime, out var start)
                && TimeOnly.TryParse(endTime, out var end))
            {
                // TimeOnly subtraction wraps around midnight, so an overnight range like
                // 22:00 → 02:00 measures 4h instead of being rejected. The draft form already
                // rolls such a range onto the next day when it converts the row to UTC; the
                // tool used to refuse to draft what the form was happy to save.
                durationMinutes = (int)(end - start).TotalMinutes;
            }

            if (durationMinutes <= 0 || durationMinutes > 1440)
                return $"Draft not submitted: {rowLabel} has an invalid duration. It must be greater than 0 and at most 1440 minutes.";

            // Project/task/tags: an id the model omits this turn falls back to the base
            // entry's id. Both fresh and carried-over ids resolve through the same known-id
            // maps (already seeded from the base draft in SeedBaseDraft), so a carried-over
            // task still gets its project cross-checked below — not just trusted blindly.
            var projectId = NonEmpty(item.ProjectId) ?? baseEntry?.ProjectId;
            var (resolvedProjectId, resolvedProjectName) = ResolveProject(projectId, item.ProjectName ?? baseEntry?.ProjectName);

            var taskId = NonEmpty(item.ProjectTaskId) ?? baseEntry?.ProjectTaskId;
            var (resolvedTaskId, resolvedTaskName, taskProjectId) = ResolveTask(taskId, item.TaskName ?? baseEntry?.TaskName);

            if (resolvedTaskId is not null && taskProjectId is not null)
            {
                if (resolvedProjectId is null)
                {
                    resolvedProjectId = taskProjectId;
                    if (_knownProjects.TryGetValue(taskProjectId.Value, out var knownProjectName))
                        resolvedProjectName = knownProjectName;
                }
                else if (resolvedProjectId != taskProjectId)
                {
                    _logger.LogWarning(
                        "Discarding task {TaskId} from LLM: does not belong to resolved project {ProjectId}",
                        resolvedTaskId, resolvedProjectId);
                    resolvedTaskId = null;
                    resolvedTaskName = null;
                }
            }

            var tagIds = item.TagIds is { Count: > 0 } ? item.TagIds : baseEntry?.TagIds ?? [];
            var (resolvedTagIds, resolvedTagNames) = ResolveTags(tagIds);

            var description = !string.IsNullOrWhiteSpace(item.Description)
                ? item.Description.Trim()
                : baseEntry?.Description;

            resolved.Add(new TimeEntryDraftItem
            {
                EntryDate = entryDate,
                StartTime = startTime,
                EndTime = endTime,
                DurationMinutes = durationMinutes,
                Description = description,
                ProjectId = resolvedProjectId,
                ProjectName = resolvedProjectName,
                ProjectTaskId = resolvedTaskId,
                TaskName = resolvedTaskName,
                TagIds = resolvedTagIds,
                TagNames = resolvedTagNames,
                // Omitted (null) falls back to the base entry's value, never to a hardcoded
                // default — otherwise an edit unrelated to billing would silently flip a
                // non-billable entry back to billable.
                IsBillable = item.IsBillable ?? baseEntry?.IsBillable ?? true
            });
        }

        CapturedDraft = new TimeEntryDraft { Entries = resolved };
        return $"Time entry draft ready for review: {resolved.Count} entr{(resolved.Count == 1 ? "y" : "ies")}. " +
               "Tell the user the draft is in the panel and they must click Create to save — do not say the entries were already created.";
    }

    private static Guid? NonEmpty(Guid? id) => id is Guid g && g != Guid.Empty ? g : null;

    [Description("Clear the current time entry draft. Call this when the user's message is not related to logging time, or when they explicitly want to discard the draft.")]
    public string ClearDraft()
    {
        CapturedDraft = null;
        DraftCleared = true;
        return "Current draft has been cleared.";
    }

    private (Guid? Id, string? Name) ResolveProject(Guid? id, string? name)
    {
        if (id is null || id == Guid.Empty)
            return (null, null);

        if (_knownProjects.TryGetValue(id.Value, out var knownName))
            return (id, string.IsNullOrWhiteSpace(name) ? knownName : name);

        _logger.LogWarning("Discarding unmatched project id from LLM: {ProjectId}", id);
        return (null, null);
    }

    private (Guid? Id, string? Name, Guid? ProjectId) ResolveTask(Guid? id, string? name)
    {
        if (id is null || id == Guid.Empty)
            return (null, null, null);

        if (_knownTasks.TryGetValue(id.Value, out var known))
            return (id, string.IsNullOrWhiteSpace(name) ? known.Name : name, known.ProjectId);

        _logger.LogWarning("Discarding unmatched task id from LLM: {TaskId}", id);
        return (null, null, null);
    }

    private (List<Guid> Ids, List<string> Names) ResolveTags(List<Guid>? ids)
    {
        var resolvedIds = new List<Guid>();
        var resolvedNames = new List<string>();

        foreach (var id in ids ?? [])
        {
            if (_knownTags.TryGetValue(id, out var name))
            {
                resolvedIds.Add(id);
                resolvedNames.Add(name);
            }
            else
            {
                _logger.LogWarning("Discarding unmatched tag id from LLM: {TagId}", id);
            }
        }

        return (resolvedIds, resolvedNames);
    }

    public IList<AITool> ToToolList()
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(SearchProjects),
            AIFunctionFactory.Create(SearchTasks),
            AIFunctionFactory.Create(SearchTags),
            AIFunctionFactory.Create(SubmitWeekTimeEntryDraft),
            AIFunctionFactory.Create(SubmitTimeEntryDraft),
            AIFunctionFactory.Create(ClearDraft)
        };
    }
}

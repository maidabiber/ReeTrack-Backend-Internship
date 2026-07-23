using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/time-entries/smart-parse")]
[Authorize]
public class SmartTimeParseController : ControllerBase
{
    private readonly ISmartTimeParseService _smartTimeParse;
    private readonly IProjectService _projectService;
    private readonly IProjectTaskService _projectTaskService;
    private readonly ITagService _tagService;

    public SmartTimeParseController(
        ISmartTimeParseService smartTimeParse,
        IProjectService projectService,
        IProjectTaskService projectTaskService,
        ITagService tagService)
    {
        _smartTimeParse = smartTimeParse;
        _projectService = projectService;
        _projectTaskService = projectTaskService;
        _tagService = tagService;
    }

    /// <summary>
    /// Parses free-form text into a structured time entry using LLM Structured Outputs.
    /// Loads active projects, open tasks, and tags for matching.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ParseSmartTimeEntryResponse>> Parse(
        [FromBody] ParseSmartTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var projects = await ListAllActiveProjectsAsync(_projectService, cancellationToken);
        var tags = await ListAllTagsAsync(_tagService, cancellationToken);
        var tasks = await ListAllOpenTasksAsync(_projectTaskService, cancellationToken);

        var catalog = new SmartTimeParseCatalog
        {
            Projects = projects
                .Select(p => new SmartTimeParseProject { Id = p.Id, Name = p.Name })
                .ToList(),
            Tasks = tasks
                .Select(t => new SmartTimeParseTask
                {
                    Id = t.Id,
                    ProjectId = t.ProjectId,
                    Name = t.Name
                })
                .ToList(),
            Tags = tags
                .Select(t => new SmartTimeParseTag { Id = t.Id, Name = t.Name })
                .ToList(),
            ReferenceDate = DateOnly.FromDateTime(DateTime.Today)
        };

        var parsed = await _smartTimeParse.ParseAsync(
            request.Text,
            catalog,
            cancellationToken);

        return Ok(new ParseSmartTimeEntryResponse
        {
            Description = parsed.Description,
            DurationMinutes = parsed.DurationMinutes,
            MatchedProjectId = parsed.MatchedProjectId,
            MatchedProjectTaskId = parsed.MatchedProjectTaskId,
            MatchedTagIds = parsed.MatchedTagIds,
            IsBillable = parsed.IsBillable,
            StartTime = parsed.StartTime,
            EndTime = parsed.EndTime,
            EntryDate = parsed.EntryDate,
            ConfidenceScore = parsed.ConfidenceScore
        });
    }

    private const int CatalogPageSize = 200;

    private static async Task<IReadOnlyList<ProjectDto>> ListAllActiveProjectsAsync(
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var all = new List<ProjectDto>();
        var page = 1;

        while (true)
        {
            var result = await projectService.ListAsync(new ProjectListQuery
            {
                Status = "active",
                Page = page,
                PageSize = CatalogPageSize,
            }, cancellationToken);

            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount || result.Items.Count == 0)
                break;

            page++;
        }

        return all;
    }

    private static async Task<IReadOnlyList<TagDto>> ListAllTagsAsync(
        ITagService tagService,
        CancellationToken cancellationToken)
    {
        var all = new List<TagDto>();
        var page = 1;

        while (true)
        {
            var result = await tagService.ListAsync(new TagListQuery
            {
                Page = page,
                PageSize = CatalogPageSize,
            }, cancellationToken);

            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount || result.Items.Count == 0)
                break;

            page++;
        }

        return all;
    }

    private static async Task<IReadOnlyList<ProjectTaskDto>> ListAllOpenTasksAsync(
        IProjectTaskService projectTaskService,
        CancellationToken cancellationToken)
    {
        var all = new List<ProjectTaskDto>();
        var page = 1;

        while (true)
        {
            var result = await projectTaskService.ListOpenAsync(new TaskListQuery
            {
                Status = "open",
                Page = page,
                PageSize = CatalogPageSize,
            }, cancellationToken);

            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount || result.Items.Count == 0)
                break;

            page++;
        }

        return all;
    }
}

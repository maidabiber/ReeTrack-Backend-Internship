using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

/// <summary>
/// Cross-project task listing for the timer project/task picker.
/// Nested create/update/delete remains under /api/projects/{projectId}/tasks.
/// </summary>
[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly IProjectTaskService _taskService;

    public TasksController(IProjectTaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskResponse>>> ListOpen(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _taskService.ListOpenAsync(new TaskListQuery
        {
            Page = page,
            PageSize = pageSize,
            Q = q
        }, cancellationToken);

        return Ok(new PagedResult<TaskResponse>
        {
            Items = result.Items.Select(ProjectTasksController.MapTask).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }
}

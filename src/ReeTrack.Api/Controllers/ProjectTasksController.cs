using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

// Trust-based domain: every authenticated user may create/edit/delete tasks
// (no Admin role gate on mutations). Changes are captured by the audit trail
// and deletes are soft-deletes guarded against tracked time.
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public class ProjectTasksController : ControllerBase
{
    private readonly IProjectTaskService _taskService;

    public ProjectTasksController(IProjectTaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskResponse>>> List(
        Guid projectId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _taskService.ListAsync(new TaskListQuery
        {
            ProjectId = projectId,
            Status = status,
            Page = page,
            PageSize = pageSize,
            Q = q
        }, cancellationToken);

        return Ok(new PagedResult<TaskResponse>
        {
            Items = result.Items.Select(MapTask).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create(
        Guid projectId,
        [FromBody] CreateTaskRequest? request,
        CancellationToken cancellationToken)
    {
        var input = new CreateTaskInput
        {
            Name = request?.Name,
            AssignedToUserId = request?.AssignedToUserId,
            TimeEstimateHours = request?.TimeEstimateHours
        };

        var task = await _taskService.CreateAsync(projectId, input, cancellationToken);
        return Ok(MapTask(task));
    }

    [HttpPost("batch")]
    public async Task<ActionResult<List<TaskResponse>>> CreateBatch(
        Guid projectId,
        [FromBody] CreateTasksBatchRequest? request,
        CancellationToken cancellationToken)
    {
        var results = new List<TaskResponse>();

        foreach (var item in request?.Tasks ?? [])
        {
            var input = new CreateTaskInput
            {
                Name = item.Name,
                AssignedToUserId = item.AssignedToUserId,
                TimeEstimateHours = item.TimeEstimateHours
            };

            var task = await _taskService.CreateAsync(projectId, input, cancellationToken);
            results.Add(MapTask(task));
        }

        return Ok(results);
    }

    [HttpPatch("{taskId:guid}")]
    public async Task<ActionResult<TaskResponse>> Update(
        Guid projectId,
        Guid taskId,
        [FromBody] UpdateTaskRequest? request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateTaskInput
        {
            Name = request?.Name,
            Status = request?.Status,
            AssignedToUserId = request?.AssignedToUserId,
            TimeEstimateHours = request?.TimeEstimateHours
        };

        var task = await _taskService.UpdateAsync(projectId, taskId, input, cancellationToken);
        return Ok(MapTask(task));
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        await _taskService.DeleteAsync(projectId, taskId, cancellationToken);
        return NoContent();
    }

    internal static TaskResponse MapTask(ProjectTaskDto task) =>
        new()
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            ClientId = task.ClientId,
            Name = task.Name,
            Status = task.Status,
            ProjectName = task.ProjectName,
            ProjectColor = task.ProjectColor,
            ClientName = task.ClientName,
            AssignedToUserId = task.AssignedToUserId,
            AssignedToName = task.AssignedToName,
            TimeEstimateHours = task.TimeEstimateHours,
            CreatedAtUtc = task.CreatedAtUtc
        };
}

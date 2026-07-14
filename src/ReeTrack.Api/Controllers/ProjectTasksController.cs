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
    public async Task<ActionResult<IReadOnlyList<TaskResponse>>> List(
        Guid projectId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var tasks = await _taskService.ListAsync(projectId, status, cancellationToken);
        return Ok(tasks.Select(MapTask).ToList());
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
            Name = task.Name,
            Status = task.Status,
            AssignedToUserId = task.AssignedToUserId,
            AssignedToName = task.AssignedToName,
            TimeEstimateHours = task.TimeEstimateHours,
            CreatedAtUtc = task.CreatedAtUtc
        };
}

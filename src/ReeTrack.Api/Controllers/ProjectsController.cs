using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

// Trust-based domain: every authenticated user may create/edit/delete projects
// (no Admin role gate on mutations). Changes are captured by the audit trail
// and deletes are soft-deletes guarded against tracked time.
[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectService.ListAsync(new ProjectListQuery
        {
            Status = status,
            ClientId = clientId,
            Page = page,
            PageSize = pageSize,
            Q = q
        }, cancellationToken);

        return Ok(new PagedResult<ProjectResponse>
        {
            Items = result.Items.Select(MapProject).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetAsync(id, cancellationToken);
        return Ok(MapProject(project));
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> Create(
        [FromBody] CreateProjectRequest? request,
        CancellationToken cancellationToken)
    {
        var input = new CreateProjectInput
        {
            Name = request?.Name,
            ClientId = request?.ClientId,
            BillingType = request?.BillingType,
            CurrencyCode = request?.CurrencyCode,
            HourlyRate = request?.HourlyRate,
            FixedFeeAmount = request?.FixedFeeAmount,
            BudgetAmount = request?.BudgetAmount,
            TimeEstimateHours = request?.TimeEstimateHours,
            Color = request?.Color
        };

        var project = await _projectService.CreateAsync(input, cancellationToken);
        return Ok(MapProject(project));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> Update(
        Guid id,
        [FromBody] UpdateProjectRequest? request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateProjectInput
        {
            Name = request?.Name,
            ClientId = request?.ClientId,
            Status = request?.Status,
            BillingType = request?.BillingType,
            CurrencyCode = request?.CurrencyCode,
            HourlyRate = request?.HourlyRate,
            FixedFeeAmount = request?.FixedFeeAmount,
            BudgetAmount = request?.BudgetAmount,
            TimeEstimateHours = request?.TimeEstimateHours,
            Color = request?.Color
        };

        var project = await _projectService.UpdateAsync(id, input, cancellationToken);
        return Ok(MapProject(project));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _projectService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    internal static ProjectResponse MapProject(ProjectDto project) =>
        new()
        {
            Id = project.Id,
            Name = project.Name,
            ClientId = project.ClientId,
            ClientName = project.ClientName,
            Status = project.Status,
            BillingType = project.BillingType,
            CurrencyCode = project.CurrencyCode,
            HourlyRate = project.HourlyRate,
            FixedFeeAmount = project.FixedFeeAmount,
            BudgetAmount = project.BudgetAmount,
            TimeEstimateHours = project.TimeEstimateHours,
            Color = project.Color,
            TaskCount = project.TaskCount,
            CreatedAtUtc = project.CreatedAtUtc
        };
}

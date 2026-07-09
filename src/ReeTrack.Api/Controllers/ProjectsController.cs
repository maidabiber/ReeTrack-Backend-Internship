using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Exceptions;
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
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        try
        {
            var projects = await _projectService.ListAsync(status, clientId, cancellationToken);
            return Ok(projects.Select(MapProject).ToList());
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectService.GetAsync(id, cancellationToken);
            return Ok(MapProject(project));
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> Create(
        [FromBody] CreateProjectRequest? request,
        CancellationToken cancellationToken)
    {
        try
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
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> Update(
        Guid id,
        [FromBody] UpdateProjectRequest? request,
        CancellationToken cancellationToken)
    {
        try
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
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _projectService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
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

public sealed class CreateProjectRequest
{
    public string? Name { get; set; }
    public Guid? ClientId { get; set; }
    public string? BillingType { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? FixedFeeAmount { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? TimeEstimateHours { get; set; }
    public string? Color { get; set; }
}

public sealed class UpdateProjectRequest
{
    public string? Name { get; set; }
    public Guid? ClientId { get; set; }
    public string? Status { get; set; }
    public string? BillingType { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? FixedFeeAmount { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? TimeEstimateHours { get; set; }
    public string? Color { get; set; }
}

public sealed class ProjectResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid ClientId { get; init; }
    public required string ClientName { get; init; }
    public required string Status { get; init; }
    public required string BillingType { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal? HourlyRate { get; init; }
    public required decimal? FixedFeeAmount { get; init; }
    public required decimal? BudgetAmount { get; init; }
    public required decimal? TimeEstimateHours { get; init; }
    public required string? Color { get; init; }
    public required int TaskCount { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

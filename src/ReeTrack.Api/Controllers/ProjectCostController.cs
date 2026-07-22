using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectCostController : ControllerBase
{
    private readonly IProjectCostService _projectCostService;

    public ProjectCostController(IProjectCostService projectCostService)
    {
        _projectCostService = projectCostService;
    }

    [HttpGet("{id:guid}/cost")]
    public async Task<ActionResult<ProjectCostResponse>> GetCost(
        Guid id,
        CancellationToken cancellationToken)
    {
        var cost = await _projectCostService.CalculateAsync(id, cancellationToken);
        return Ok(Map(cost));
    }

    internal static ProjectCostResponse Map(ProjectCostDto cost) =>
        new()
        {
            ProjectId = cost.ProjectId,
            CalculatedCost = cost.CalculatedCost,
            CalculatedAtUtc = cost.CalculatedAtUtc
        };
}

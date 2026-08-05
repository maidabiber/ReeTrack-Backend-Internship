using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/thresholds")]
[Authorize(Roles = "Admin")]
public class ProjectThresholdsController : ControllerBase
{
    private readonly IProjectThresholdService _thresholdService;

    public ProjectThresholdsController(IProjectThresholdService thresholdService)
    {
        _thresholdService = thresholdService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectThresholdResponse>>> List(
        Guid projectId,
        [FromQuery] ProjectThresholdMetricType? metricType,
        CancellationToken cancellationToken)
    {
        var thresholds = await _thresholdService.ListAsync(projectId, metricType, cancellationToken);
        return Ok(thresholds.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ProjectThresholdResponse>> Create(
        Guid projectId,
        [FromBody] CreateProjectThresholdRequest? request,
        CancellationToken cancellationToken)
    {
        var created = await _thresholdService.CreateAsync(
            projectId,
            new CreateProjectThresholdInput
            {
                MetricType = request?.MetricType ?? ProjectThresholdMetricType.Cost,
                ThresholdPercentage = request?.ThresholdPercentage ?? 0m
            },
            cancellationToken);

        return Ok(Map(created));
    }

    [HttpPut("{thresholdId:guid}")]
    public async Task<ActionResult<ProjectThresholdResponse>> Update(
        Guid projectId,
        Guid thresholdId,
        [FromBody] UpdateProjectThresholdRequest? request,
        CancellationToken cancellationToken)
    {
        var updated = await _thresholdService.UpdateAsync(
            projectId,
            thresholdId,
            new UpdateProjectThresholdInput
            {
                ThresholdPercentage = request?.ThresholdPercentage ?? 0m
            },
            cancellationToken);

        return Ok(Map(updated));
    }

    [HttpDelete("{thresholdId:guid}")]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid thresholdId,
        CancellationToken cancellationToken)
    {
        await _thresholdService.DeleteAsync(projectId, thresholdId, cancellationToken);
        return NoContent();
    }

    private static ProjectThresholdResponse Map(ProjectThresholdDto dto) =>
        new()
        {
            Id = dto.Id,
            ProjectId = dto.ProjectId,
            MetricType = dto.MetricType,
            ThresholdPercentage = dto.ThresholdPercentage,
            IsTriggered = dto.IsTriggered,
            CreatedAtUtc = dto.CreatedAtUtc,
            UpdatedAtUtc = dto.UpdatedAtUtc
        };
}

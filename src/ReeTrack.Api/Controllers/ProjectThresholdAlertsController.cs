using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/project-threshold-alerts")]
[Authorize(Roles = "Admin")]
public class ProjectThresholdAlertsController : ControllerBase
{
    private readonly IProjectThresholdEvaluationService _evaluationService;
    private readonly IProjectThresholdDeliveryService _deliveryService;

    public ProjectThresholdAlertsController(
        IProjectThresholdEvaluationService evaluationService,
        IProjectThresholdDeliveryService deliveryService)
    {
        _evaluationService = evaluationService;
        _deliveryService = deliveryService;
    }

    /// <summary>
    /// Manually recalculates project cost/time usage and queues/delivers threshold alerts.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<ProjectThresholdRunResponse>> Run(
        [FromBody] RunProjectThresholdAlertsRequest? request,
        CancellationToken cancellationToken)
    {
        var deliverImmediately = request?.DeliverImmediately ?? false;
        var summary = await _evaluationService.EvaluateAsync(
            request?.ProjectId,
            deliverImmediately,
            cancellationToken);

        if (deliverImmediately)
            summary.NotificationsDelivered = await _deliveryService.DeliverPendingAsync(cancellationToken);

        return Ok(new ProjectThresholdRunResponse
        {
            ProjectsEvaluated = summary.ProjectsEvaluated,
            ThresholdsTriggered = summary.ThresholdsTriggered,
            ThresholdsCleared = summary.ThresholdsCleared,
            PendingCreated = summary.PendingCreated,
            NotificationsDelivered = summary.NotificationsDelivered
        });
    }
}

using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Interfaces;

public interface IProjectThresholdEvaluationService
{
    /// <summary>
    /// Recalculates project cost and time usage and queues alerts for newly crossed thresholds.
    /// </summary>
    Task<ProjectThresholdRunSummary> EvaluateAsync(
        Guid? projectId = null,
        bool deliverImmediately = false,
        CancellationToken cancellationToken = default);
}

public interface IProjectThresholdDeliveryService
{
    /// <summary>
    /// Delivers pending alerts whose DeliverAfterUtc has passed.
    /// </summary>
    Task<int> DeliverPendingAsync(CancellationToken cancellationToken = default);
}

public interface IProjectThresholdService
{
    Task<IReadOnlyList<ProjectThresholdDto>> ListAsync(
        Guid projectId,
        ProjectThresholdMetricType? metricType = null,
        CancellationToken cancellationToken = default);

    Task<ProjectThresholdDto> CreateAsync(
        Guid projectId,
        CreateProjectThresholdInput input,
        CancellationToken cancellationToken = default);

    Task<ProjectThresholdDto> UpdateAsync(
        Guid projectId,
        Guid thresholdId,
        UpdateProjectThresholdInput input,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid projectId,
        Guid thresholdId,
        CancellationToken cancellationToken = default);
}

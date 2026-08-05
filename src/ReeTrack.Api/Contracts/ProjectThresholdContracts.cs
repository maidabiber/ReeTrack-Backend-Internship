using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Contracts;

public sealed class ProjectThresholdResponse
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required ProjectThresholdMetricType MetricType { get; init; }
    public required decimal ThresholdPercentage { get; init; }
    public required bool IsTriggered { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}

public sealed class CreateProjectThresholdRequest
{
    public ProjectThresholdMetricType MetricType { get; init; }
    public decimal ThresholdPercentage { get; init; }
}

public sealed class UpdateProjectThresholdRequest
{
    public decimal ThresholdPercentage { get; init; }
}

public sealed class RunProjectThresholdAlertsRequest
{
    public Guid? ProjectId { get; init; }
    public bool DeliverImmediately { get; init; }
}

public sealed class ProjectThresholdRunResponse
{
    public required int ProjectsEvaluated { get; init; }
    public required int ThresholdsTriggered { get; init; }
    public required int ThresholdsCleared { get; init; }
    public required int PendingCreated { get; init; }
    public required int NotificationsDelivered { get; init; }
}

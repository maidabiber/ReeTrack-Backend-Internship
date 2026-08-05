using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Models;

public sealed class ProjectThresholdRunSummary
{
    public int ProjectsEvaluated { get; set; }
    public int ThresholdsTriggered { get; set; }
    public int ThresholdsCleared { get; set; }
    public int PendingCreated { get; set; }
    public int NotificationsDelivered { get; set; }
}

public sealed class ProjectThresholdDto
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required ProjectThresholdMetricType MetricType { get; init; }
    public required decimal ThresholdPercentage { get; init; }
    public required bool IsTriggered { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}

public sealed class CreateProjectThresholdInput
{
    public ProjectThresholdMetricType MetricType { get; init; }
    public decimal ThresholdPercentage { get; init; }
}

public sealed class UpdateProjectThresholdInput
{
    public decimal ThresholdPercentage { get; init; }
}

public sealed class RunProjectThresholdAlertsInput
{
    public Guid? ProjectId { get; init; }
    public bool DeliverImmediately { get; init; }
}

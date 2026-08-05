using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Events;

namespace ReeTrack.Application.Notifications.Events;

public sealed class ProjectThresholdAlertNotification : IDomainEvent
{
    public required Guid RecipientUserId { get; init; }
    public required string RecipientName { get; init; }
    public required Guid ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required ProjectThresholdMetricType MetricType { get; init; }
    public required decimal ThresholdPercentage { get; init; }

    // Cost snapshot (used when MetricType == Cost)
    public required decimal CostPercentage { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required decimal FixedFeeAmount { get; init; }
    public required string CurrencyCode { get; init; }

    // Time snapshot (used when MetricType == TimeEstimate)
    public required decimal HoursPercentage { get; init; }
    public required decimal ActualHours { get; init; }
    public required decimal TimeEstimateHours { get; init; }
}

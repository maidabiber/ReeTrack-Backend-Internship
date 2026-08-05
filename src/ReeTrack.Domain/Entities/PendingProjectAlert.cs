using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class PendingProjectAlert : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid ThresholdId { get; set; }
    public ProjectThresholdMetricType MetricType { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public decimal ThresholdPercentage { get; set; }

    // Cost snapshot (used when MetricType == Cost)
    public decimal CostPercentage { get; set; }
    public decimal CalculatedCost { get; set; }
    public decimal FixedFeeAmount { get; set; }
    public string CurrencyCode { get; set; } = "EUR";

    // Time snapshot (used when MetricType == TimeEstimate)
    public decimal HoursPercentage { get; set; }
    public decimal ActualHours { get; set; }
    public decimal TimeEstimateHours { get; set; }

    public DateTime DeliverAfterUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }

    public Project Project { get; set; } = null!;
    public ProjectThreshold Threshold { get; set; } = null!;
}

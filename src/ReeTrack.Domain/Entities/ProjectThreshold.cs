using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class ProjectThreshold : BaseEntity
{
    public Guid ProjectId { get; set; }
    public ProjectThresholdMetricType MetricType { get; set; }
    public decimal ThresholdPercentage { get; set; }
    public bool IsTriggered { get; set; }

    public Project Project { get; set; } = null!;
}

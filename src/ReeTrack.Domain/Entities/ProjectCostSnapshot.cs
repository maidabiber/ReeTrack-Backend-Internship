using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class ProjectCostSnapshot : BaseEntity
{
    public Guid ProjectId { get; set; }
    public decimal CalculatedCost { get; set; }
    public DateTime CalculatedAtUtc { get; set; }

    public Project Project { get; set; } = null!;
}

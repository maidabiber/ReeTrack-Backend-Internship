using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class ProjectTaskCostSnapshot : BaseEntity
{
    public Guid ProjectCostSnapshotId { get; set; }
    public Guid ProjectTaskId { get; set; }
    public decimal CalculatedCost { get; set; }
    public decimal TotalHours { get; set; }
    public decimal WeekendHours { get; set; }
    public decimal HolidayHours { get; set; }
    public decimal OvertimeHours { get; set; }

    public ProjectCostSnapshot ProjectCostSnapshot { get; set; } = null!;
    public ProjectTask ProjectTask { get; set; } = null!;
}

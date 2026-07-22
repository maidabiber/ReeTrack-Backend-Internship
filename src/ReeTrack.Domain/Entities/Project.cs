using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class Project : BaseEntity, ISoftDeletable, IAuditable
{
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public Guid CreatedByUserId { get; set; }

    public string CurrencyCode { get; set; } = "EUR";
    public decimal? FixedFeeAmount { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? TimeEstimateHours { get; set; }
    public string? Color { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Client Client { get; set; } = null!;
    public ICollection<ProjectTask> Tasks { get; set; } = [];
    public ICollection<ProjectCostSnapshot> CostSnapshots { get; set; } = [];
}

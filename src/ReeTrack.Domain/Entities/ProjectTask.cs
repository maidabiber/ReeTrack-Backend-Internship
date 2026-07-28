using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class ProjectTask : BaseEntity, ISoftDeletable, IAuditable
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProjectTaskStatus Status { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public decimal? TimeEstimateHours { get; set; }

    public ExternalProvider? ExternalProvider { get; set; }
    public string? ExternalId { get; set; }
    public string? ExternalKey { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Project Project { get; set; } = null!;
    public User? AssignedToUser { get; set; }
}

using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class TimeEntry : BaseEntity, ISoftDeletable
{
    public Guid UserId { get; set; }

    public string? Description { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProjectTaskId { get; set; }
    public bool IsBillable { get; set; }

    public TimeEntryMode Mode { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int DurationSeconds { get; set; }

    public TimeEntryStatus Status { get; set; } = TimeEntryStatus.Confirmed;
    public Guid? SubmittedByUserId { get; set; }
    public Guid? ShareGroupId { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public User User { get; set; } = null!;
    public User? SubmittedByUser { get; set; }
    public Client? Client { get; set; }
    public Project? Project { get; set; }
    public ProjectTask? ProjectTask { get; set; }
    public ICollection<TimeEntryTag> TimeEntryTags { get; set; } = [];
}

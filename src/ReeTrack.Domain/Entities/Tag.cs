using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class Tag : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public ICollection<TimeEntryTag> TimeEntryTags { get; set; } = [];
}

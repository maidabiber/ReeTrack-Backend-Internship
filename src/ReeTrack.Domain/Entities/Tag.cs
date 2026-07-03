using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class Tag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }

    public ICollection<TimeEntryTag> TimeEntryTags { get; set; } = [];
}

using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class TimeEntryTemplateTag 
{
    public Guid TimeEntryTemplateId { get; set; }
    public Guid TagId { get; set; }

    public TimeEntryTemplate TimeEntryTemplate { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

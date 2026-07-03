namespace ReeTrack.Domain.Entities;

public class TimeEntryTag
{
    public Guid TimeEntryId { get; set; }
    public Guid TagId { get; set; }

    public TimeEntry TimeEntry { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

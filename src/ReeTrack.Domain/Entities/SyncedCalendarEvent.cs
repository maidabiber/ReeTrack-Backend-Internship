using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class SyncedCalendarEvent : BaseEntity
{
    public Guid ConnectionId { get; set; }
    public string ExternalEventId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? Location { get; set; }
    public string? HtmlLink { get; set; }
    public DateTime? RawUpdatedAtUtc { get; set; }

    public UserCalendarConnection Connection { get; set; } = null!;
}

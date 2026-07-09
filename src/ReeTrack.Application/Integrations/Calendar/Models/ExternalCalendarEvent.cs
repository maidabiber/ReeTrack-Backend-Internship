namespace ReeTrack.Application.Integrations.Calendar.Models;

public class ExternalCalendarEvent
{
    public string ExternalEventId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public bool IsAllDay { get; init; }
    public string? Location { get; init; }
    public string? HtmlLink { get; init; }
    public DateTime? RawUpdatedAtUtc { get; init; }
}

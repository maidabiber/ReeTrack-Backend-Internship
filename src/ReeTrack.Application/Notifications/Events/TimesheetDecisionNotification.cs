using ReeTrack.Domain.Events;

namespace ReeTrack.Application.Notifications.Events;

public sealed class TimesheetDecisionNotification : IDomainEvent
{
    public required Guid TimesheetId { get; init; }
    public required Guid RecipientUserId { get; init; }
    public required string RecipientName { get; init; }
    public required string ReviewerName { get; init; }
    public required DateOnly WeekStartDate { get; init; }
    public required bool Approved { get; init; }
    public string? Comment { get; init; }
}

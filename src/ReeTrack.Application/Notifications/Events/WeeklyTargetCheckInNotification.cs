using ReeTrack.Domain.Events;

namespace ReeTrack.Application.Notifications.Events;

public sealed class WeeklyTargetCheckInNotification : IDomainEvent
{
    public required Guid RecipientUserId { get; init; }
    public required string RecipientName { get; init; }
    public required decimal LoggedHours { get; init; }
    public required decimal TargetHours { get; init; }
    public required decimal RemainingHours { get; init; }
    public required bool OnTrack { get; init; }
    public required DateOnly WeekStartDate { get; init; }
    public required DateOnly TimesheetWeekStartDate { get; init; }
    public DateOnly? WeakestDay { get; init; }
    public decimal? WeakestDayHours { get; init; }
}

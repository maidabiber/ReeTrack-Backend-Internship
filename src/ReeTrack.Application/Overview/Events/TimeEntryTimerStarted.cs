using ReeTrack.Domain.Events;

namespace ReeTrack.Application.Overview.Events;

public sealed class TimeEntryTimerStarted : IDomainEvent
{
    public required Guid TimeEntryId { get; init; }
    public required Guid UserId { get; init; }
    public required DateTime StartedAtUtc { get; init; }
}

using ReeTrack.Domain.Events;

namespace ReeTrack.Application.Overview.Events;

public sealed class TimeEntryTimerStopped : IDomainEvent
{
    public required Guid TimeEntryId { get; init; }
    public required Guid UserId { get; init; }
    public required long AddedSeconds { get; init; }
}

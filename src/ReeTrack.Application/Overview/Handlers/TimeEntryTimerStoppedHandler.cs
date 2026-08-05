using ReeTrack.Application.Notifications;
using ReeTrack.Application.Overview.Events;

namespace ReeTrack.Application.Overview.Handlers;

public sealed class TimeEntryTimerStoppedHandler : IDomainEventHandler<TimeEntryTimerStopped>
{
    private readonly IOverviewRealtimePublisher _publisher;

    public TimeEntryTimerStoppedHandler(IOverviewRealtimePublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task HandleAsync(TimeEntryTimerStopped domainEvent, CancellationToken cancellationToken = default)
    {
        await _publisher.PublishTimerStoppedAsync(
            domainEvent.UserId,
            domainEvent.TimeEntryId,
            domainEvent.AddedSeconds,
            cancellationToken);
    }
}

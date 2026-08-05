using ReeTrack.Application.Notifications;
using ReeTrack.Application.Overview.Events;

namespace ReeTrack.Application.Overview.Handlers;

public sealed class TimeEntryTimerStartedHandler : IDomainEventHandler<TimeEntryTimerStarted>
{
    private readonly IOverviewRealtimePublisher _publisher;

    public TimeEntryTimerStartedHandler(IOverviewRealtimePublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task HandleAsync(TimeEntryTimerStarted domainEvent, CancellationToken cancellationToken = default)
    {
        await _publisher.PublishTimerStartedAsync(domainEvent.TimeEntryId, cancellationToken);
    }
}

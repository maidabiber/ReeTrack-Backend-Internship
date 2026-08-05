namespace ReeTrack.Application.Overview;

/// <summary>
/// Pushes overview events to connected clients in real time.
/// </summary>
public interface IOverviewRealtimePublisher
{
    Task PublishTimerStartedAsync(Guid timeEntryId, CancellationToken cancellationToken = default);

    Task PublishTimerStoppedAsync(
        Guid userId, Guid timeEntryId, long addedSeconds, CancellationToken cancellationToken = default);

    Task PublishTimerUpdatedAsync(Guid timeEntryId, CancellationToken cancellationToken = default);
}

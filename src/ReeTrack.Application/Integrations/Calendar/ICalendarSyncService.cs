namespace ReeTrack.Application.Integrations.Calendar;

public interface ICalendarSyncService
{
    Task SyncConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task SyncStaleConnectionsAsync(CancellationToken cancellationToken = default);
}

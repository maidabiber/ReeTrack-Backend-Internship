using Microsoft.AspNetCore.SignalR;
using ReeTrack.Api.Hubs;
using ReeTrack.Application.Overview;

namespace ReeTrack.Api.Realtime;

/// <summary>
/// SignalR-backed publisher for overview realtime pushes.
/// </summary>
public sealed class SignalROverviewRealtimePublisher : IOverviewRealtimePublisher
{
    private readonly IHubContext<OverviewHub> _hubContext;

    public SignalROverviewRealtimePublisher(IHubContext<OverviewHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishTimerStartedAsync(
        Guid timeEntryId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group("overview:admins")
                .SendAsync("TimerStarted", new { TimeEntryId = timeEntryId }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Could be logged
        }
    }

    public async Task PublishTimerStoppedAsync(
        Guid userId, Guid timeEntryId, long addedSeconds, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group("overview:admins")
                .SendAsync("TimerStopped", new { UserId = userId, TimeEntryId = timeEntryId, AddedSeconds = addedSeconds }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Could be logged
        }
    }

    public async Task PublishTimerUpdatedAsync(
        Guid timeEntryId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group("overview:admins")
                .SendAsync("TimerUpdated", new { TimeEntryId = timeEntryId }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Could be logged
        }
    }
}

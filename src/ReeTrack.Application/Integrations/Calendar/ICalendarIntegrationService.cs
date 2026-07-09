using ReeTrack.Application.Integrations.Calendar.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Integrations.Calendar;

public interface ICalendarIntegrationService
{
    string GenerateState();
    string ValidateReturnUrl(string? returnUrl);
    string BuildConnectUrl(CalendarProviderType providerType, string state);
    Task<CalendarConnectionDto> CompleteConnectAsync(
        Guid userId,
        CalendarProviderType providerType,
        string code,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarConnectionDto>> ListConnectionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task DisconnectAsync(Guid userId, Guid connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncedCalendarEventDto>> GetEventsAsync(
        Guid userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);
    Task TriggerSyncIfStaleAsync(Guid userId, CancellationToken cancellationToken = default);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Integrations.Calendar;

public class CalendarSyncService : ICalendarSyncService
{
    private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromMinutes(5);
    private const int MaxSyncErrorLength = 2000;

    private readonly IApplicationDbContext _db;
    private readonly ICalendarProviderRegistry _providerRegistry;
    private readonly ITokenProtector _tokenProtector;
    private readonly CalendarSyncOptions _syncOptions;
    private readonly ILogger<CalendarSyncService> _logger;

    public CalendarSyncService(
        IApplicationDbContext db,
        ICalendarProviderRegistry providerRegistry,
        ITokenProtector tokenProtector,
        IOptions<CalendarSyncOptions> syncOptions,
        ILogger<CalendarSyncService> logger)
    {
        _db = db;
        _providerRegistry = providerRegistry;
        _tokenProtector = tokenProtector;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task SyncConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _db.UserCalendarConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);

        if (connection is null)
            throw new CalendarIntegrationException("Calendar connection not found.", 404);

        if (connection.SyncStatus == CalendarSyncStatus.Syncing)
            return;

        connection.SyncStatus = CalendarSyncStatus.Syncing;
        connection.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var provider = _providerRegistry.GetProvider(connection.ProviderType);
            var accessToken = _tokenProtector.Unprotect(connection.AccessToken);
            var refreshToken = _tokenProtector.Unprotect(connection.RefreshToken);

            if (connection.ExpirationDateTime <= DateTime.UtcNow.Add(TokenRefreshBuffer))
            {
                var refreshed = await provider.RefreshAccessTokenAsync(refreshToken, cancellationToken);
                accessToken = refreshed.AccessToken;
                connection.AccessToken = _tokenProtector.Protect(refreshed.AccessToken);
                connection.RefreshToken = _tokenProtector.Protect(refreshed.RefreshToken);
                connection.ExpirationDateTime = refreshed.ExpiresAtUtc;
                if (!string.IsNullOrWhiteSpace(refreshed.ProviderAccountId))
                    connection.ProviderAccountId = refreshed.ProviderAccountId;
            }

            var fromUtc = DateTime.UtcNow.AddDays(-_syncOptions.LookbackDays);
            var toUtc = DateTime.UtcNow.AddDays(_syncOptions.LookaheadDays);
            var externalEvents = await provider.FetchEventsAsync(accessToken, fromUtc, toUtc, cancellationToken);

            var existingEvents = await _db.SyncedCalendarEvents
                .Where(e => e.ConnectionId == connection.Id)
                .Where(e => e.StartAtUtc < toUtc && e.EndAtUtc > fromUtc)
                .ToListAsync(cancellationToken);

            var existingByExternalId = existingEvents.ToDictionary(e => e.ExternalEventId);
            var seenExternalIds = new HashSet<string>();
            var now = DateTime.UtcNow;

            foreach (var externalEvent in externalEvents)
            {
                seenExternalIds.Add(externalEvent.ExternalEventId);

                if (existingByExternalId.TryGetValue(externalEvent.ExternalEventId, out var existing))
                {
                    existing.Title = externalEvent.Title;
                    existing.Description = externalEvent.Description;
                    existing.StartAtUtc = externalEvent.StartAtUtc;
                    existing.EndAtUtc = externalEvent.EndAtUtc;
                    existing.IsAllDay = externalEvent.IsAllDay;
                    existing.Location = externalEvent.Location;
                    existing.HtmlLink = externalEvent.HtmlLink;
                    existing.RawUpdatedAtUtc = externalEvent.RawUpdatedAtUtc;
                    existing.UpdatedAtUtc = now;
                }
                else
                {
                    _db.SyncedCalendarEvents.Add(new SyncedCalendarEvent
                    {
                        Id = Guid.NewGuid(),
                        ConnectionId = connection.Id,
                        ExternalEventId = externalEvent.ExternalEventId,
                        Title = externalEvent.Title,
                        Description = externalEvent.Description,
                        StartAtUtc = externalEvent.StartAtUtc,
                        EndAtUtc = externalEvent.EndAtUtc,
                        IsAllDay = externalEvent.IsAllDay,
                        Location = externalEvent.Location,
                        HtmlLink = externalEvent.HtmlLink,
                        RawUpdatedAtUtc = externalEvent.RawUpdatedAtUtc,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
                }
            }

            var toRemove = existingEvents
                .Where(e => !seenExternalIds.Contains(e.ExternalEventId))
                .ToList();

            if (toRemove.Count > 0)
                _db.SyncedCalendarEvents.RemoveRange(toRemove);

            connection.LastSyncedAtUtc = now;
            connection.SyncStatus = CalendarSyncStatus.Idle;
            connection.LastSyncError = null;
            connection.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync calendar connection {ConnectionId}", connectionId);

            connection.SyncStatus = CalendarSyncStatus.Error;
            connection.LastSyncError = Truncate(ex.Message, MaxSyncErrorLength);
            connection.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            throw;
        }
    }

    public async Task SyncStaleConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var staleThreshold = DateTime.UtcNow.AddMinutes(-_syncOptions.IntervalMinutes);

        var connectionIds = await _db.UserCalendarConnections
            .AsNoTracking()
            .Where(c => c.LastSyncedAtUtc == null || c.LastSyncedAtUtc < staleThreshold)
            .Where(c => c.SyncStatus != CalendarSyncStatus.Syncing)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var connectionId in connectionIds)
        {
            try
            {
                await SyncConnectionAsync(connectionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background sync failed for connection {ConnectionId}", connectionId);
            }
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

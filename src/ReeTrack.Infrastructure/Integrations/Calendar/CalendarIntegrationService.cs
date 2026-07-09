using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Application.Integrations.Calendar.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Integrations.Calendar;

public class CalendarIntegrationService : ICalendarIntegrationService
{
    private static readonly HashSet<string> AllowedReturnPaths =
        new(StringComparer.OrdinalIgnoreCase) { "/", "/integrations", "/profile", "/signin" };

    private readonly IApplicationDbContext _db;
    private readonly ICalendarProviderRegistry _providerRegistry;
    private readonly ICalendarSyncService _syncService;
    private readonly ITokenProtector _tokenProtector;
    private readonly CalendarSyncOptions _syncOptions;

    public CalendarIntegrationService(
        IApplicationDbContext db,
        ICalendarProviderRegistry providerRegistry,
        ICalendarSyncService syncService,
        ITokenProtector tokenProtector,
        IOptions<CalendarSyncOptions> syncOptions)
    {
        _db = db;
        _providerRegistry = providerRegistry;
        _syncService = syncService;
        _tokenProtector = tokenProtector;
        _syncOptions = syncOptions.Value;
    }

    public string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public string ValidateReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//"))
            return "/";

        var path = returnUrl.Split('?', '#')[0];
        return AllowedReturnPaths.Contains(path) ? path : "/";
    }

    public string BuildConnectUrl(CalendarProviderType providerType, string state)
    {
        var provider = _providerRegistry.GetProvider(providerType);
        return provider.BuildAuthorizationUrl(state);
    }

    public async Task<CalendarConnectionDto> CompleteConnectAsync(
        Guid userId,
        CalendarProviderType providerType,
        string code,
        CancellationToken cancellationToken = default)
    {
        var provider = _providerRegistry.GetProvider(providerType);
        var tokenSet = await provider.ExchangeCodeAsync(code, cancellationToken);
        var now = DateTime.UtcNow;

        var connection = await _db.UserCalendarConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProviderType == providerType, cancellationToken);

        if (connection is null)
        {
            connection = new UserCalendarConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProviderType = providerType,
                CreatedAtUtc = now
            };
            _db.UserCalendarConnections.Add(connection);
        }

        connection.AccessToken = _tokenProtector.Protect(tokenSet.AccessToken);
        connection.RefreshToken = _tokenProtector.Protect(tokenSet.RefreshToken);
        connection.ExpirationDateTime = tokenSet.ExpiresAtUtc;
        connection.ProviderAccountId = tokenSet.ProviderAccountId;
        connection.SyncStatus = CalendarSyncStatus.Idle;
        connection.LastSyncError = null;
        connection.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _syncService.SyncConnectionAsync(connection.Id, cancellationToken);

        var refreshed = await _db.UserCalendarConnections
            .AsNoTracking()
            .FirstAsync(c => c.Id == connection.Id, cancellationToken);

        return MapConnection(refreshed);
    }

    public async Task<IReadOnlyList<CalendarConnectionDto>> ListConnectionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var connections = await _db.UserCalendarConnections
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.ProviderType)
            .ToListAsync(cancellationToken);

        return connections.Select(MapConnection).ToList();
    }

    public async Task DisconnectAsync(Guid userId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _db.UserCalendarConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.UserId == userId, cancellationToken);

        if (connection is null)
            throw new CalendarIntegrationException("Calendar connection not found.", 404);

        _db.UserCalendarConnections.Remove(connection);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncedCalendarEventDto>> GetEventsAsync(
        Guid userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-_syncOptions.LookbackDays);
        var to = toUtc ?? DateTime.UtcNow.AddDays(_syncOptions.LookaheadDays);

        var events = await _db.SyncedCalendarEvents
            .AsNoTracking()
            .Where(e => e.Connection.UserId == userId)
            .Where(e => e.StartAtUtc < to && e.EndAtUtc > from)
            .OrderBy(e => e.StartAtUtc)
            .Select(e => new SyncedCalendarEventDto
            {
                Id = e.Id,
                ConnectionId = e.ConnectionId,
                ExternalEventId = e.ExternalEventId,
                Title = e.Title,
                Description = e.Description,
                StartAtUtc = e.StartAtUtc,
                EndAtUtc = e.EndAtUtc,
                IsAllDay = e.IsAllDay,
                Location = e.Location,
                HtmlLink = e.HtmlLink
            })
            .ToListAsync(cancellationToken);

        return events;
    }

    public async Task TriggerSyncIfStaleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var staleThreshold = DateTime.UtcNow.AddMinutes(-_syncOptions.IntervalMinutes);

        var staleConnectionIds = await _db.UserCalendarConnections
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Where(c => c.LastSyncedAtUtc == null || c.LastSyncedAtUtc < staleThreshold)
            .Where(c => c.SyncStatus != CalendarSyncStatus.Syncing)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var connectionId in staleConnectionIds)
        {
            try
            {
                await _syncService.SyncConnectionAsync(connectionId, cancellationToken);
            }
            catch
            {
                // Best-effort stale sync; cached data is still returned.
            }
        }
    }

    private static CalendarConnectionDto MapConnection(UserCalendarConnection connection) =>
        new()
        {
            Id = connection.Id,
            ProviderType = connection.ProviderType,
            ProviderAccountId = connection.ProviderAccountId,
            LastSyncedAtUtc = connection.LastSyncedAtUtc,
            SyncStatus = connection.SyncStatus,
            LastSyncError = connection.LastSyncError,
            CreatedAtUtc = connection.CreatedAtUtc
        };
}

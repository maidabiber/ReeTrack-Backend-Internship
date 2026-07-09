using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Application.Integrations.Calendar.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Integrations.Calendar;
using ReeTrack.Infrastructure.Persistence;
using Xunit;

namespace ReeTrack.UnitTests.Integrations.Calendar;

public class CalendarSyncServiceTests
{
    [Fact]
    public async Task SyncConnectionAsync_UpsertsAndRemovesStaleEvents()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.UserCalendarConnections.Add(new UserCalendarConnection
        {
            Id = connectionId,
            UserId = userId,
            ProviderType = CalendarProviderType.Google,
            AccessToken = "protected-access",
            RefreshToken = "protected-refresh",
            ExpirationDateTime = now.AddHours(1),
            SyncStatus = CalendarSyncStatus.Idle,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        db.SyncedCalendarEvents.Add(new SyncedCalendarEvent
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            ExternalEventId = "stale-event",
            Title = "Old",
            StartAtUtc = now,
            EndAtUtc = now.AddHours(1),
            IsAllDay = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await db.SaveChangesAsync();

        var provider = new FakeCalendarProvider(
        [
            new ExternalCalendarEvent
            {
                ExternalEventId = "event-1",
                Title = "Standup",
                StartAtUtc = now.AddDays(1),
                EndAtUtc = now.AddDays(1).AddMinutes(30),
                IsAllDay = false
            }
        ]);

        var service = new CalendarSyncService(
            db,
            new CalendarProviderRegistry([provider]),
            new PassthroughTokenProtector(),
            Options.Create(new CalendarSyncOptions { LookbackDays = 30, LookaheadDays = 90 }),
            NullLogger<CalendarSyncService>.Instance);

        await service.SyncConnectionAsync(connectionId);

        var events = await db.SyncedCalendarEvents
            .Where(e => e.ConnectionId == connectionId)
            .ToListAsync();

        Assert.Single(events);
        Assert.Equal("event-1", events[0].ExternalEventId);
        Assert.Equal("Standup", events[0].Title);

        var connection = await db.UserCalendarConnections.SingleAsync(c => c.Id == connectionId);
        Assert.Equal(CalendarSyncStatus.Idle, connection.SyncStatus);
        Assert.NotNull(connection.LastSyncedAtUtc);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class PassthroughTokenProtector : ITokenProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string protectedText) => protectedText;
    }

    private sealed class FakeCalendarProvider(IReadOnlyList<ExternalCalendarEvent> events) : ICalendarProvider
    {
        public CalendarProviderType ProviderType => CalendarProviderType.Google;

        public string BuildAuthorizationUrl(string state) => string.Empty;

        public Task<OAuthTokenSet> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<OAuthTokenSet> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(new OAuthTokenSet
            {
                AccessToken = "access",
                RefreshToken = refreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            });

        public Task<IReadOnlyList<ExternalCalendarEvent>> FetchEventsAsync(
            string accessToken,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(events);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Integrations.Calendar;
using ReeTrack.Infrastructure.Persistence;
using Xunit;

namespace ReeTrack.UnitTests.Integrations.Calendar;

public class CalendarIntegrationServiceTests
{
    [Fact]
    public async Task ListConnectionsAsync_DoesNotExposeTokenFields()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.UserCalendarConnections.Add(new UserCalendarConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProviderType = CalendarProviderType.Google,
            AccessToken = "secret-access",
            RefreshToken = "secret-refresh",
            ExpirationDateTime = now.AddHours(1),
            ProviderAccountId = "user@example.com",
            SyncStatus = CalendarSyncStatus.Idle,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();

        var service = new CalendarIntegrationService(
            db,
            new CalendarProviderRegistry([]),
            new NoOpCalendarSyncService(),
            new PassthroughTokenProtector(),
            Options.Create(new CalendarSyncOptions()));

        var connections = await service.ListConnectionsAsync(userId);

        Assert.Single(connections);
        Assert.Equal("user@example.com", connections[0].ProviderAccountId);
        Assert.Equal(CalendarProviderType.Google, connections[0].ProviderType);

        var json = System.Text.Json.JsonSerializer.Serialize(connections[0]);
        Assert.DoesNotContain("secret-access", json);
        Assert.DoesNotContain("secret-refresh", json);
        Assert.DoesNotContain("AccessToken", json);
        Assert.DoesNotContain("RefreshToken", json);
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

    private sealed class NoOpCalendarSyncService : ICalendarSyncService
    {
        public Task SyncConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SyncStaleConnectionsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

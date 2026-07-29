using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Notifications;
using ReeTrack.Domain.Events;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.TimeEntries;

namespace ReeTrack.UnitTests.TimeEntries;

internal sealed class NoOpDomainEventPublisher : IDomainEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent =>
        Task.CompletedTask;
}

internal static class TimeEntryServiceTestDependencies
{
    public static (
        IConfiguration Configuration,
        IOptions<AppOptions> AppOptions,
        IDomainEventPublisher EventPublisher) Create()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:Origin"] = "http://localhost:5173"
            })
            .Build();

        var appOptions = Options.Create(new AppOptions());
        return (configuration, appOptions, new NoOpDomainEventPublisher());
    }

    public static TimeEntryService CreateTimeEntryService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ITimeEntryGuardService entryGuard) =>
        new(
            db,
            currentUser,
            entryGuard,
            new TimeEntryAssociationService(db));

    public static SharedTimeEntryService CreateSharedTimeEntryService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ITimeEntryGuardService entryGuard,
        IDomainEventPublisher eventPublisher)
    {
        var associations = new TimeEntryAssociationService(db);
        var timeEntries = new TimeEntryService(db, currentUser, entryGuard, associations);
        return new SharedTimeEntryService(db, currentUser, timeEntries, eventPublisher, entryGuard, associations);
    }
}

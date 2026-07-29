using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Events;

namespace ReeTrack.Application.Notifications;

/// <summary>
/// Resolves and executes all <see cref="IDomainEventHandler{TEvent}"/> instances concurrently,
/// each in its own DI scope so scoped dependencies (e.g. DbContext) remain thread-safe.
/// </summary>
public sealed class DomainEventPublisher : IDomainEventPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DomainEventPublisher(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await using var probeScope = _scopeFactory.CreateAsyncScope();
        var handlers = probeScope.ServiceProvider
            .GetServices<IDomainEventHandler<TEvent>>()
            .ToList();

        if (handlers.Count == 0)
            return;

        if (handlers.Count == 1)
        {
            await using var singleScope = _scopeFactory.CreateAsyncScope();
            var handler = singleScope.ServiceProvider.GetRequiredService(handlers[0].GetType());
            await ((IDomainEventHandler<TEvent>)handler).HandleAsync(domainEvent, cancellationToken);
            return;
        }

        var handlerTypes = handlers.Select(h => h.GetType()).ToList();

        await Task.WhenAll(handlerTypes.Select(async handlerType =>
        {
            await using var handlerScope = _scopeFactory.CreateAsyncScope();
            var handler = (IDomainEventHandler<TEvent>)handlerScope.ServiceProvider
                .GetRequiredService(handlerType);
            await handler.HandleAsync(domainEvent, cancellationToken);
        }));
    }
}

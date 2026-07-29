using ReeTrack.Domain.Events;

namespace ReeTrack.Application.Notifications;

/// <summary>
/// Publishes domain events to all registered <see cref="IDomainEventHandler{TEvent}"/> instances.
/// </summary>
public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;
}

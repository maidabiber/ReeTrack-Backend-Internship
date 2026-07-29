using ReeTrack.Domain.Events;

namespace ReeTrack.Application.Notifications;

/// <summary>
/// Handles a specific domain event type when published through <see cref="IDomainEventPublisher"/>.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

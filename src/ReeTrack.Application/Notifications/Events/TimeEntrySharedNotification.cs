using ReeTrack.Domain.Events;

namespace ReeTrack.Application.Notifications.Events;

public sealed class TimeEntrySharedNotification : IDomainEvent
{
    public required Guid EntryId { get; init; }
    public required Guid AssigneeUserId { get; init; }
    public required string AssigneeName { get; init; }
    public required string SubmitterName { get; init; }
    public string? Description { get; init; }
}

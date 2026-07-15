using ReeTrack.Domain.Entities;

namespace ReeTrack.Application.Common.Interfaces;

public interface ISharedTimeEntryEmailNotifier
{
    void QueueShareNotificationEmails(
        IReadOnlyList<TimeEntry> createdEntries,
        IReadOnlyDictionary<Guid, User> assigneeById,
        string submitterName);
}

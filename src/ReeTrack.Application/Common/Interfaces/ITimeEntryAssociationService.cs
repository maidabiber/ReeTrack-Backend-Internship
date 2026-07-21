using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITimeEntryAssociationService
{
    Task ApplyForCreateAsync(
        TimeEntry entry,
        TimeEntryFieldsInput input,
        CancellationToken cancellationToken = default);

    Task ApplyForUpdateAsync(
        TimeEntry entry,
        TimeEntryFieldsInput input,
        CancellationToken cancellationToken = default);

    void CopyAssociations(TimeEntry source, TimeEntry target);
}

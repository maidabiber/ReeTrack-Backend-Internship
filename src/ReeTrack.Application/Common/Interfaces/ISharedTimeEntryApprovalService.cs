using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ISharedTimeEntryApprovalService
{
    Task<IReadOnlyList<TimeEntryDto>> ListPendingAsync(CancellationToken cancellationToken = default);

    Task<UpdateTimeEntryResult> UpdatePendingEntryAsync(
        Guid entryId,
        UpdatePendingEntryInput input,
        CancellationToken cancellationToken = default);

    Task<TimeEntryDto> ApprovePendingEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);
}

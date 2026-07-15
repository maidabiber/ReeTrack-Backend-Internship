using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ISharedTimeEntryService
{
    Task<CreateSharedManualEntryResult> StopSharedTimerAsync(
        StopSharedTimerInput input,
        CancellationToken cancellationToken = default);

    Task<CreateSharedManualEntryResult> CreateSharedManualEntryAsync(
        CreateSharedManualEntryInput input,
        CancellationToken cancellationToken = default);

    Task<CreateSharedManualEntryResult> CreateSharedDurationOnlyEntryAsync(
        CreateSharedDurationOnlyEntryInput input,
        CancellationToken cancellationToken = default);

    Task<CreateSharedManualEntryResult> ShareExistingEntryAsync(
        Guid entryId,
        ShareExistingEntryInput input,
        CancellationToken cancellationToken = default);
}

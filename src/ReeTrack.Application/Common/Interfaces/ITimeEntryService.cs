using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITimeEntryService
{
    Task<TimeEntryDto?> GetActiveTimerAsync(CancellationToken cancellationToken = default);

    Task<TimeEntryDto> StartTimerAsync(
        StartTimerInput input,
        CancellationToken cancellationToken = default);

    Task<TimeEntryDto> StopTimerAsync(
        StopTimerInput? input = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CreateManualEntryResult> CreateManualEntryAsync(
        CreateManualEntryInput input,
        CancellationToken cancellationToken = default);

    Task<CreateManualEntryResult> CreateDurationOnlyEntryAsync(
        CreateDurationOnlyEntryInput input,
        CancellationToken cancellationToken = default);

    Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(
        Guid entryId,
        UpdateTimeEntryInput input,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListByDateRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<UpdateTimeEntryResult> UpdateDurationOnlyEntryAsync(
        Guid entryId,
        UpdateDurationOnlyEntryInput input,
        CancellationToken cancellationToken = default);
}

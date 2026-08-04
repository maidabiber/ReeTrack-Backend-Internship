using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITimeEntryService
{
    Task<TimeEntryDto?> GetActiveTimerAsync(CancellationToken cancellationToken = default);

    Task<StopTimerResultDto> StopTimerAsync(
        TimeEntryInput? input = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid entryId, CancellationToken cancellationToken = default);

    Task<TimeEntryDto> CreateAsync(
        TimeEntryInput input,
        CancellationToken cancellationToken = default);

    Task<TimeEntryDto> UpdateAsync(
        Guid entryId,
        TimeEntryInput input,
        CancellationToken cancellationToken = default);

    Task<TimeEntryDto> ShareEntryAsync(
        Guid entryId,
        IReadOnlyList<Guid> assigneeUserIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> CreateAndShareAsync(
        TimeEntryInput input,
        IReadOnlyList<Guid> assigneeUserIds,
        CancellationToken cancellationToken = default);

    Task<TimeEntryDto> ApprovePendingEntryAsync(
        Guid entryId,
        TimeEntryInput? input = null,
        CancellationToken cancellationToken = default);

    Task RejectPendingEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListByDateRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListPendingEntriesAsync(CancellationToken cancellationToken = default);
}

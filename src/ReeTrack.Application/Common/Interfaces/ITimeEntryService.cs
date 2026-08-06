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

    /// <summary>
    /// Creates several entries as one unit. Every row is checked for overlaps up front, so a
    /// collision on the last row can't leave the earlier ones committed. When any row conflicts
    /// and <paramref name="skipOverlapping"/> is false, nothing is written and the conflicts are
    /// returned for the caller to review; when it is true, the clean rows are created and the
    /// conflicting ones are reported as skipped.
    /// </summary>
    Task<BatchCreateTimeEntriesResultDto> CreateBatchAsync(
        IReadOnlyList<TimeEntryInput> inputs,
        bool skipOverlapping,
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

    Task<PagedResult<TimeEntryDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        string? date = null,
        string sort = "newest",
        int? utcOffsetMinutes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListByDateRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListPendingEntriesAsync(CancellationToken cancellationToken = default);
}

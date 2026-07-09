using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITimeEntryService
{
    Task<TimeEntryDto?> GetActiveTimerAsync(CancellationToken cancellationToken = default);

    Task<TimeEntryDto> StartTimerAsync(
        string? description,
        bool isBillable = true,
        CancellationToken cancellationToken = default);

    Task<TimeEntryDto> StopTimerAsync(
        string? description = null,
        CancellationToken cancellationToken = default);

    Task<CreateSharedManualEntryResult> StopSharedTimerAsync(
        IReadOnlyList<Guid> assigneeUserIds,
        string? description = null,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CreateManualEntryResult> CreateManualEntryAsync(
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable = true,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default);

    Task<CreateManualEntryResult> CreateDurationOnlyEntryAsync(
        string? description,
        DateTime entryDateUtc,
        int durationSeconds,
        bool isBillable = true,
        CancellationToken cancellationToken = default);

    Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(
        Guid entryId,
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListByDateRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<UpdateTimeEntryResult> UpdateDurationOnlyEntryAsync(
        Guid entryId,
        string? description,
        DateTime entryDateUtc,
        int durationSeconds,
        bool isBillable,
        CancellationToken cancellationToken = default);

    Task<CreateSharedManualEntryResult> CreateSharedManualEntryAsync(
        IReadOnlyList<Guid> assigneeUserIds,
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable = true,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default);

    Task<CreateSharedManualEntryResult> ShareExistingEntryAsync(
        Guid entryId,
        IReadOnlyList<Guid> assigneeUserIds,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> ListPendingAsync(CancellationToken cancellationToken = default);

    Task<UpdateTimeEntryResult> UpdatePendingEntryAsync(
        Guid entryId,
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default);

    Task<TimeEntryDto> ApprovePendingEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);
}

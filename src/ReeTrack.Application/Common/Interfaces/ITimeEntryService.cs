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

    Task<IReadOnlyList<TimeEntryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CreateManualEntryResult> CreateManualEntryAsync(
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable = true,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default);

    Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(
        Guid entryId,
        string? description,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        bool isBillable,
        bool confirmOverlap = false,
        CancellationToken cancellationToken = default);
}

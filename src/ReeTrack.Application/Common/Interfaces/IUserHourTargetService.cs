using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Interfaces;

public interface IUserHourTargetService
{
    Task<UserHourTargetDto?> GetOverrideAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserHourTargetDto> UpsertOverrideAsync(
        Guid userId,
        HourTargetMode mode,
        decimal targetHours,
        CancellationToken cancellationToken = default);

    Task ClearOverrideAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<EffectiveHourTargetDto> GetEffectiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

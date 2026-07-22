using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IUserHourlyRateService
{
    Task<IReadOnlyList<UserHourlyRateDto>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserHourlyRateDto> GetCurrentAsync(
        Guid userId,
        DateOnly? onDate = null,
        CancellationToken cancellationToken = default);

    Task<UserHourlyRateDto> ChangeAsync(
        Guid userId,
        ChangeUserHourlyRateInput input,
        CancellationToken cancellationToken = default);

    Task<UserHourlyRateDto> CorrectAsync(
        Guid userId,
        Guid rateId,
        CorrectUserHourlyRateInput input,
        CancellationToken cancellationToken = default);
}

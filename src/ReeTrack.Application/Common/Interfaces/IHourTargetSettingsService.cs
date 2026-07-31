using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IHourTargetSettingsService
{
    Task<HourTargetSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<HourTargetSettingsDto> UpdateAsync(
        HourTargetSettingsDto settings,
        CancellationToken cancellationToken = default);
}

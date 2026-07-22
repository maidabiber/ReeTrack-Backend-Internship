using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IRateMultiplierSettingsService
{
    Task<RateMultiplierSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<RateMultiplierSettingsDto> UpdateAsync(
        RateMultiplierSettingsDto settings,
        CancellationToken cancellationToken = default);
}

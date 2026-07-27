using ReeTrack.Domain.Services;

namespace ReeTrack.Application.Common.Interfaces;

/// <summary>
/// Supplies the configured weekend / holiday / overtime premiums to anything that
/// costs time, falling back to <see cref="RateMultiplierConfig.Defaults"/> when the
/// workspace has never saved settings.
/// </summary>
public interface IRateMultiplierConfigProvider
{
    Task<RateMultiplierConfig> GetAsync(CancellationToken cancellationToken = default);
}

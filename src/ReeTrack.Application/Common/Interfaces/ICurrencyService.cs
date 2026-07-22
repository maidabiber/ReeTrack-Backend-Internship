using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ICurrencyService
{
    Task<IReadOnlyList<CurrencyDto>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Empty/whitespace → default EUR (must be active). Otherwise requires an active catalog row.
    /// Returns the normalized uppercase code.
    /// </summary>
    Task<string> EnsureSupportedAsync(string? currencyCode, CancellationToken cancellationToken = default);
}

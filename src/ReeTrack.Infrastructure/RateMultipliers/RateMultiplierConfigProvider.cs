using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Services;

namespace ReeTrack.Infrastructure.RateMultipliers;

/// <inheritdoc cref="IRateMultiplierConfigProvider"/>
public sealed class RateMultiplierConfigProvider : IRateMultiplierConfigProvider
{
    private readonly IApplicationDbContext _db;

    public RateMultiplierConfigProvider(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<RateMultiplierConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.RateMultiplierSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
            return RateMultiplierConfig.Defaults;

        return new RateMultiplierConfig(
            settings.WeekendPremium,
            settings.HolidayPremium,
            settings.OvertimePremium,
            settings.WeeklyOvertimeThresholdHours);
    }
}

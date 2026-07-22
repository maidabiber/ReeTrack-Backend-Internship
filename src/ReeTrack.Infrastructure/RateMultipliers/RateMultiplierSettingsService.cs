using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Persistence.Configurations;

namespace ReeTrack.Infrastructure.RateMultipliers;

public sealed class RateMultiplierSettingsService : IRateMultiplierSettingsService
{
    private readonly IApplicationDbContext _db;

    public RateMultiplierSettingsService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<RateMultiplierSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await EnsureSettingsAsync(cancellationToken);
        return ToDto(settings);
    }

    public async Task<RateMultiplierSettingsDto> UpdateAsync(
        RateMultiplierSettingsDto request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var settings = await EnsureSettingsAsync(cancellationToken);
        settings.WeekendPremium = request.WeekendPremium;
        settings.HolidayPremium = request.HolidayPremium;
        settings.OvertimePremium = request.OvertimePremium;
        settings.WeeklyOvertimeThresholdHours = request.WeeklyOvertimeThresholdHours;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    private async Task<RateMultiplierSettings> EnsureSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.RateMultiplierSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is not null)
            return settings;

        var now = DateTime.UtcNow;
        var defaults = RateMultiplierConfig.Defaults;
        settings = new RateMultiplierSettings
        {
            Id = RateMultiplierSettingsConfiguration.DefaultSettingsId,
            WeekendPremium = defaults.WeekendPremium,
            HolidayPremium = defaults.HolidayPremium,
            OvertimePremium = defaults.OvertimePremium,
            WeeklyOvertimeThresholdHours = defaults.WeeklyOvertimeThresholdHours,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.RateMultiplierSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static void Validate(RateMultiplierSettingsDto request)
    {
        if (request.WeekendPremium < 0m)
            throw new AppException("Weekend premium must be zero or greater.", 400);
        if (request.HolidayPremium < 0m)
            throw new AppException("Holiday premium must be zero or greater.", 400);
        if (request.OvertimePremium < 0m)
            throw new AppException("Overtime premium must be zero or greater.", 400);
        if (request.WeeklyOvertimeThresholdHours <= 0m)
            throw new AppException("Weekly overtime threshold must be greater than zero.", 400);
    }

    private static RateMultiplierSettingsDto ToDto(RateMultiplierSettings settings) =>
        new()
        {
            WeekendPremium = settings.WeekendPremium,
            HolidayPremium = settings.HolidayPremium,
            OvertimePremium = settings.OvertimePremium,
            WeeklyOvertimeThresholdHours = settings.WeeklyOvertimeThresholdHours
        };
}

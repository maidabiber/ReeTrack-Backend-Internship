using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Constants;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Exceptions;

namespace ReeTrack.Infrastructure.HourTargets;

public sealed class HourTargetSettingsService : IHourTargetSettingsService
{
    private readonly IApplicationDbContext _db;

    public HourTargetSettingsService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<HourTargetSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await EnsureSettingsAsync(cancellationToken);
        return ToDto(settings);
    }

    public async Task<HourTargetSettingsDto> UpdateAsync(
        HourTargetSettingsDto request,
        CancellationToken cancellationToken = default)
    {
        var settings = await EnsureSettingsAsync(cancellationToken);

        try
        {
            settings.Update(request.Mode, request.TargetHours);
            settings.UpdatedAtUtc = DateTime.UtcNow;
        }
        catch (DomainException ex)
        {
            throw new AppException(ex.Message, 400);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    private async Task<HourTargetSettings> EnsureSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.HourTargetSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is not null)
            return settings;

        settings = HourTargetSettings.CreateDefault(HourTargetDefaults.SettingsId, DateTime.UtcNow);
        _db.HourTargetSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static HourTargetSettingsDto ToDto(HourTargetSettings settings) =>
        new()
        {
            Mode = settings.Mode,
            TargetHours = settings.TargetHours
        };
}

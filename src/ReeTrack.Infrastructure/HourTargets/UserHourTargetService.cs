using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Exceptions;
using ReeTrack.Domain.Services;

namespace ReeTrack.Infrastructure.HourTargets;

public sealed class UserHourTargetService : IUserHourTargetService
{
    private readonly IApplicationDbContext _db;
    private readonly IHourTargetSettingsService _settingsService;

    public UserHourTargetService(
        IApplicationDbContext db,
        IHourTargetSettingsService settingsService)
    {
        _db = db;
        _settingsService = settingsService;
    }

    public async Task<UserHourTargetDto?> GetOverrideAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        var target = await _db.UserHourTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        return target is null ? null : ToDto(target);
    }

    public async Task<UserHourTargetDto> UpsertOverrideAsync(
        Guid userId,
        HourTargetMode mode,
        decimal targetHours,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        var existing = await _db.UserHourTargets
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        var now = DateTime.UtcNow;

        try
        {
            if (existing is null)
            {
                existing = UserHourTarget.Create(userId, mode, targetHours, now);
                _db.UserHourTargets.Add(existing);
            }
            else
            {
                existing.Update(mode, targetHours);
                existing.UpdatedAtUtc = now;
            }
        }
        catch (DomainException ex)
        {
            throw new AppException(ex.Message, 400, ErrorCode.Validation);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(existing);
    }

    public async Task ClearOverrideAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        var existing = await _db.UserHourTargets
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (existing is null)
            return;

        _db.UserHourTargets.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<EffectiveHourTargetDto> GetEffectiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        var overrideTarget = await _db.UserHourTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        HourTargetMode mode;
        decimal targetHours;
        var isOverride = overrideTarget is not null;

        if (overrideTarget is not null)
        {
            mode = overrideTarget.Mode;
            targetHours = overrideTarget.TargetHours;
        }
        else
        {
            var defaults = await _settingsService.GetAsync(cancellationToken);
            mode = defaults.Mode;
            targetHours = defaults.TargetHours;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var holidayFrom = today.AddDays(-90);
        var holidayTo = today.AddDays(90);

        var holidayDates = await _db.Holidays
            .AsNoTracking()
            .Where(h => h.IsActive && h.Date >= holidayFrom && h.Date <= holidayTo)
            .Select(h => h.Date)
            .ToListAsync(cancellationToken);

        var holidaySet = holidayDates.ToHashSet();
        var isWorkdayToday = WorkingDayCalendar.IsWorkday(today, holidaySet);

        return new EffectiveHourTargetDto
        {
            Mode = mode,
            TargetHours = targetHours,
            IsOverride = isOverride,
            IsWorkdayToday = isWorkdayToday,
            HolidayDates = holidayDates
                .Select(d => d.ToString("yyyy-MM-dd"))
                .OrderBy(d => d)
                .ToList()
        };
    }

    private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
            throw AppErrors.NotFound("User");
    }

    private static UserHourTargetDto ToDto(UserHourTarget target) =>
        new()
        {
            UserId = target.UserId,
            Mode = target.Mode,
            TargetHours = target.TargetHours
        };
}

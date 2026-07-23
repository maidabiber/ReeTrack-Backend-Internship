using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence.Configurations;

namespace ReeTrack.Infrastructure.Holidays;

public sealed class HolidayService : IHolidayService
{
    private readonly IApplicationDbContext _db;
    private readonly INagerDateClient _nagerDateClient;

    public HolidayService(IApplicationDbContext db, INagerDateClient nagerDateClient)
    {
        _db = db;
        _nagerDateClient = nagerDateClient;
    }

    public Task<IReadOnlyList<HolidayCalendarDto>> ListCalendarsAsync(
        CancellationToken cancellationToken = default) =>
        _nagerDateClient.GetAvailableCountriesAsync(cancellationToken);

    public async Task<HolidayCalendarSettingsDto> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await EnsureSettingsAsync(cancellationToken);
        return ToSettingsDto(settings);
    }

    public async Task<HolidayCalendarSettingsDto> UpdateSettingsAsync(
        string? countryCode,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCountryCode(countryCode);
        var settings = await EnsureSettingsAsync(cancellationToken);
        var previousCountry = settings.CountryCode;

        if (normalized is null)
        {
            if (previousCountry is not null)
            {
                await RemoveAllCalendarHolidaysAsync(cancellationToken);
                settings.CountryCode = null;
                settings.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return ToSettingsDto(settings);
        }

        // Validate country exists before mutating
        var calendars = await _nagerDateClient.GetAvailableCountriesAsync(cancellationToken);
        if (!calendars.Any(c => string.Equals(c.CountryCode, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new AppException("Unknown holiday calendar country code.", 400);

        var countryChanged = !string.Equals(previousCountry, normalized, StringComparison.OrdinalIgnoreCase);

        // Fetch first so a Nager failure does not wipe existing holidays
        var fetched = await FetchSyncedYearsAsync(normalized, cancellationToken);

        if (countryChanged)
        {
            await RemoveAllCalendarHolidaysAsync(cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        settings.CountryCode = normalized;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await ApplyCalendarSyncAsync(normalized, fetched, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return ToSettingsDto(settings);
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var settings = await EnsureSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.CountryCode))
            throw new AppException("Select a holiday calendar before refreshing.", 400);

        var countryCode = settings.CountryCode;
        var fetched = await FetchSyncedYearsAsync(countryCode, cancellationToken);
        await ApplyCalendarSyncAsync(countryCode, fetched, cancellationToken);
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HolidayDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var holidays = await _db.Holidays
            .AsNoTracking()
            .OrderBy(h => h.Date)
            .ThenBy(h => h.Name)
            .ToListAsync(cancellationToken);

        return holidays.Select(ToDto).ToList();
    }

    public async Task<HolidayDto> CreateCustomAsync(
        CreateCustomHolidayRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new AppException("Holiday name is required.", 400);
        if (name.Length > 200)
            throw new AppException("Holiday name must be 200 characters or fewer.", 400);

        var exists = await _db.Holidays.AnyAsync(h => h.Date == request.Date, cancellationToken);
        if (exists)
            throw new AppException("A holiday already exists on that date.", 409);

        var now = DateTime.UtcNow;
        var holiday = new Holiday
        {
            Id = Guid.NewGuid(),
            Date = request.Date,
            Name = name,
            IsActive = true,
            Source = HolidaySource.Custom,
            CountryCode = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Holidays.Add(holiday);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(holiday);
    }

    public async Task<HolidayDto> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var holiday = await _db.Holidays.FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            ?? throw new AppException("Holiday was not found.", 404);

        holiday.IsActive = isActive;
        holiday.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(holiday);
    }

    public async Task DeleteCustomAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var holiday = await _db.Holidays.FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            ?? throw new AppException("Holiday was not found.", 404);

        if (holiday.Source != HolidaySource.Custom)
            throw new AppException("Calendar holidays cannot be deleted. Deactivate them instead.", 400);

        _db.Holidays.Remove(holiday);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<NagerPublicHoliday>> FetchSyncedYearsAsync(
        string countryCode,
        CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var current = await _nagerDateClient.GetPublicHolidaysAsync(year, countryCode, cancellationToken);
        var next = await _nagerDateClient.GetPublicHolidaysAsync(year + 1, countryCode, cancellationToken);

        return current
            .Concat(next)
            .GroupBy(h => h.Date)
            .Select(g => g.First())
            .ToList();
    }

    private async Task ApplyCalendarSyncAsync(
        string countryCode,
        IReadOnlyList<NagerPublicHoliday> fetched,
        CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var rangeStart = new DateOnly(year, 1, 1);
        var rangeEnd = new DateOnly(year + 1, 12, 31);
        var fetchedByDate = fetched
            .Where(h => h.Date >= rangeStart && h.Date <= rangeEnd)
            .GroupBy(h => h.Date)
            .ToDictionary(g => g.Key, g => g.First());

        var existing = await _db.Holidays
            .Where(h => h.Date >= rangeStart && h.Date <= rangeEnd)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var occupiedDates = new HashSet<DateOnly>();

        foreach (var holiday in existing)
        {
            if (holiday.Source == HolidaySource.Custom)
            {
                occupiedDates.Add(holiday.Date);
                continue;
            }

            if (!fetchedByDate.TryGetValue(holiday.Date, out var remote))
            {
                _db.Holidays.Remove(holiday);
                continue;
            }

            holiday.Name = remote.Name;
            holiday.CountryCode = countryCode;
            holiday.UpdatedAtUtc = now;
            occupiedDates.Add(holiday.Date);
        }

        foreach (var remote in fetchedByDate.Values)
        {
            if (!occupiedDates.Add(remote.Date))
                continue;

            _db.Holidays.Add(new Holiday
            {
                Id = Guid.NewGuid(),
                Date = remote.Date,
                Name = remote.Name,
                IsActive = true,
                Source = HolidaySource.Calendar,
                CountryCode = countryCode,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
    }

    private async Task RemoveAllCalendarHolidaysAsync(CancellationToken cancellationToken)
    {
        var calendarHolidays = await _db.Holidays
            .Where(h => h.Source == HolidaySource.Calendar)
            .ToListAsync(cancellationToken);

        _db.Holidays.RemoveRange(calendarHolidays);
    }

    private async Task<HolidayCalendarSettings> EnsureSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.HolidayCalendarSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is not null)
            return settings;

        var now = DateTime.UtcNow;
        settings = new HolidayCalendarSettings
        {
            Id = HolidayCalendarSettingsConfiguration.DefaultSettingsId,
            CountryCode = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.HolidayCalendarSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return null;

        var normalized = countryCode.Trim().ToUpperInvariant();
        if (normalized.Length != 2)
            throw new AppException("Country code must be a 2-letter ISO code.", 400);

        return normalized;
    }

    private static HolidayCalendarSettingsDto ToSettingsDto(HolidayCalendarSettings settings) =>
        new() { CountryCode = settings.CountryCode };

    private static HolidayDto ToDto(Holiday holiday) =>
        new()
        {
            Id = holiday.Id,
            Date = holiday.Date,
            Name = holiday.Name,
            IsActive = holiday.IsActive,
            Source = holiday.Source == HolidaySource.Calendar ? "calendar" : "custom",
            CountryCode = holiday.CountryCode
        };
}

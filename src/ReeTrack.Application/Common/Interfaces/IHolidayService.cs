using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IHolidayService
{
    Task<IReadOnlyList<HolidayCalendarDto>> ListCalendarsAsync(
        CancellationToken cancellationToken = default);

    Task<HolidayCalendarSettingsDto> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    Task<HolidayCalendarSettingsDto> UpdateSettingsAsync(
        string? countryCode,
        CancellationToken cancellationToken = default);

    Task SyncAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HolidayDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<HolidayDto> CreateCustomAsync(
        CreateCustomHolidayRequestDto request,
        CancellationToken cancellationToken = default);

    Task<HolidayDto> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteCustomAsync(Guid id, CancellationToken cancellationToken = default);
}

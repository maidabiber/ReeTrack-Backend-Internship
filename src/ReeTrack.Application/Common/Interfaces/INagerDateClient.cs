using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface INagerDateClient
{
    Task<IReadOnlyList<HolidayCalendarDto>> GetAvailableCountriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NagerPublicHoliday>> GetPublicHolidaysAsync(
        int year,
        string countryCode,
        CancellationToken cancellationToken = default);
}

public sealed class NagerPublicHoliday
{
    public required DateOnly Date { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> Types { get; init; } = Array.Empty<string>();
}

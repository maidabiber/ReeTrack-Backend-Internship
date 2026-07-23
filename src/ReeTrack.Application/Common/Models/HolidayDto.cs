namespace ReeTrack.Application.Common.Models;

public sealed class HolidayDto
{
    public required Guid Id { get; init; }
    public required DateOnly Date { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required string Source { get; init; }
    public string? CountryCode { get; init; }
}

public sealed class HolidayCalendarSettingsDto
{
    public string? CountryCode { get; init; }
}

public sealed class HolidayCalendarDto
{
    public required string CountryCode { get; init; }
    public required string Name { get; init; }
}

public sealed class CreateCustomHolidayRequestDto
{
    public required DateOnly Date { get; init; }
    public required string Name { get; init; }
}

public sealed class UpdateHolidayActiveRequestDto
{
    public required bool IsActive { get; init; }
}

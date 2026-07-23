namespace ReeTrack.Api.Contracts;

public sealed class HolidayResponse
{
    public required Guid Id { get; init; }
    public required DateOnly Date { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required string Source { get; init; }
    public string? CountryCode { get; init; }
}

public sealed class HolidayCalendarResponse
{
    public required string CountryCode { get; init; }
    public required string Name { get; init; }
}

public sealed class HolidayCalendarSettingsResponse
{
    public string? CountryCode { get; init; }
}

public sealed class UpdateHolidayCalendarSettingsRequest
{
    public string? CountryCode { get; init; }
}

public sealed class CreateCustomHolidayRequest
{
    public required DateOnly Date { get; init; }
    public required string Name { get; init; }
}

public sealed class UpdateHolidayActiveRequest
{
    public required bool IsActive { get; init; }
}

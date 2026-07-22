namespace ReeTrack.Api.Contracts;

public sealed class RateMultiplierSettingsResponse
{
    public required decimal WeekendPremium { get; init; }
    public required decimal HolidayPremium { get; init; }
    public required decimal OvertimePremium { get; init; }
    public required decimal WeeklyOvertimeThresholdHours { get; init; }
}

public sealed class UpdateRateMultiplierSettingsRequest
{
    public required decimal WeekendPremium { get; init; }
    public required decimal HolidayPremium { get; init; }
    public required decimal OvertimePremium { get; init; }
    public required decimal WeeklyOvertimeThresholdHours { get; init; }
}

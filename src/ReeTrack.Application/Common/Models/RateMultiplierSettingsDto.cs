namespace ReeTrack.Application.Common.Models;

public sealed class RateMultiplierSettingsDto
{
    public required decimal WeekendPremium { get; init; }
    public required decimal HolidayPremium { get; init; }
    public required decimal OvertimePremium { get; init; }
    public required decimal WeeklyOvertimeThresholdHours { get; init; }
}

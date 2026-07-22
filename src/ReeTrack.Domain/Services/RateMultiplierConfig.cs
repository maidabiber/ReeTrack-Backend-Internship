namespace ReeTrack.Domain.Services;

public sealed record RateMultiplierConfig(
    decimal WeekendPremium,
    decimal HolidayPremium,
    decimal OvertimePremium,
    decimal WeeklyOvertimeThresholdHours)
{
    public static RateMultiplierConfig Defaults { get; } = new(0.5m, 1.0m, 0.5m, 40m);
}

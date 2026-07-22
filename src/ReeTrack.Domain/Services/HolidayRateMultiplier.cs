namespace ReeTrack.Domain.Services;

public sealed class HolidayRateMultiplier : IRateMultiplier
{
    public int ExecutionOrder => 20;

    public decimal Apply(decimal currentRate, RateContext context) =>
        context.IsHoliday
            ? currentRate + (context.BaseRate * context.MultiplierConfig.HolidayPremium)
            : currentRate;
}

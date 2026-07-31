namespace ReeTrack.Domain.Services;

public sealed class WeekendRateMultiplier : IRateMultiplier
{
    public int ExecutionOrder => 10;

    public decimal Apply(decimal currentRate, RateContext context) =>
        WorkingDayCalendar.IsWeekend(context.EntryDate)
            ? currentRate + (context.BaseRate * context.MultiplierConfig.WeekendPremium)
            : currentRate;
}

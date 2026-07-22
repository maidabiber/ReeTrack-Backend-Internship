namespace ReeTrack.Domain.Services;

public sealed class WeekendRateMultiplier : IRateMultiplier
{
    public int ExecutionOrder => 10;

    public decimal Apply(decimal currentRate, RateContext context) =>
        context.EntryDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? currentRate + (context.BaseRate * context.MultiplierConfig.WeekendPremium)
            : currentRate;
}

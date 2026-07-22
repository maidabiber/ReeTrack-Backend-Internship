namespace ReeTrack.Domain.Services;

public sealed class BaseRateMultiplier : IRateMultiplier
{
    public int ExecutionOrder => 0;

    public decimal Apply(decimal currentRate, RateContext context) => currentRate;
}

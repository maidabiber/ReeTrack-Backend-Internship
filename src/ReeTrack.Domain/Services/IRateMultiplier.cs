namespace ReeTrack.Domain.Services;

public interface IRateMultiplier
{
    int ExecutionOrder { get; }

    decimal Apply(decimal currentRate, RateContext context);
}

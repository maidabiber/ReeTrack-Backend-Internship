namespace ReeTrack.Domain.Services;

public sealed class OvertimeRateMultiplier : IRateMultiplier
{
    public int ExecutionOrder => 30;

    public decimal Apply(decimal currentRate, RateContext context)
    {
        var entryHours = context.TimeEntry.DurationSeconds / 3600m;
        if (entryHours <= 0m)
            return currentRate;

        var threshold = context.MultiplierConfig.WeeklyOvertimeThresholdHours;
        var regularHours = Math.Clamp(
            threshold - context.CumulativeWeeklyHoursBeforeEntry,
            0m,
            entryHours);
        var overtimeHours = entryHours - regularHours;
        if (overtimeHours <= 0m)
            return currentRate;

        return currentRate
            + (context.BaseRate * context.MultiplierConfig.OvertimePremium * (overtimeHours / entryHours));
    }
}

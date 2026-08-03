using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Services;

public sealed record WeeklyTargetProgress(
    decimal LoggedHours,
    decimal TargetHours,
    decimal RemainingHours,
    bool OnTrack,
    DateOnly WeekStartLocal,
    DateOnly? WeakestDay,
    decimal WeakestDayHours);

/// <summary>
/// Pure week-vs-target progress for Friday check-ins (org timezone calendar week).
/// </summary>
public static class WeeklyTargetProgressCalculator
{
    public static WeeklyTargetProgress Calculate(
        HourTargetMode mode,
        decimal targetHours,
        IReadOnlyDictionary<DateOnly, int> loggedSecondsByLocalDate,
        DateOnly weekStartLocal,
        DateOnly todayLocal,
        IReadOnlySet<DateOnly> holidays)
    {
        if (todayLocal < weekStartLocal)
            todayLocal = weekStartLocal;

        var weekEndLocal = weekStartLocal.AddDays(6);
        var targetWorkdayEnd = weekEndLocal;
        var weekTargetHours = mode == HourTargetMode.Weekly
            ? targetHours
            : targetHours * WorkingDayCalendar.CountWorkdaysInRange(
                weekStartLocal,
                targetWorkdayEnd,
                holidays);

        var loggedSeconds = 0;
        foreach (var (date, seconds) in loggedSecondsByLocalDate)
        {
            if (date >= weekStartLocal && date <= weekEndLocal)
                loggedSeconds += seconds;
        }

        var loggedHours = loggedSeconds / 3600m;
        var remainingHours = Math.Max(0m, weekTargetHours - loggedHours);
        var onTrack = loggedHours >= weekTargetHours;

        DateOnly? weakestDay = null;
        var weakestSeconds = int.MaxValue;
        for (var date = weekStartLocal; date <= todayLocal && date <= weekEndLocal; date = date.AddDays(1))
        {
            if (!WorkingDayCalendar.IsWorkday(date, holidays))
                continue;

            var seconds = loggedSecondsByLocalDate.GetValueOrDefault(date);
            if (seconds < weakestSeconds)
            {
                weakestSeconds = seconds;
                weakestDay = date;
            }
        }

        return new WeeklyTargetProgress(
            Math.Round(loggedHours, 2, MidpointRounding.AwayFromZero),
            Math.Round(weekTargetHours, 2, MidpointRounding.AwayFromZero),
            Math.Round(remainingHours, 2, MidpointRounding.AwayFromZero),
            onTrack,
            weekStartLocal,
            weakestDay,
            weakestDay is null
                ? 0m
                : Math.Round(weakestSeconds / 3600m, 2, MidpointRounding.AwayFromZero));
    }
}

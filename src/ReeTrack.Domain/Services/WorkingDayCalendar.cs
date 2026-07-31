namespace ReeTrack.Domain.Services;

public static class WorkingDayCalendar
{
    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static bool IsWorkday(DateOnly date, IReadOnlySet<DateOnly> holidays) =>
        !IsWeekend(date) && !holidays.Contains(date);

    public static int CountWorkdaysInRange(
        DateOnly startInclusive,
        DateOnly endInclusive,
        IReadOnlySet<DateOnly> holidays)
    {
        if (endInclusive < startInclusive)
            return 0;

        var count = 0;
        for (var date = startInclusive; date <= endInclusive; date = date.AddDays(1))
        {
            if (IsWorkday(date, holidays))
                count++;
        }

        return count;
    }
}

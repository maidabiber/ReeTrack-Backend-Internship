namespace ReeTrack.Infrastructure.Timesheets;

public static class TimesheetWeek
{
    /// <summary>UTC Monday of the week containing the given instant.</summary>
    public static DateOnly ToWeekStart(DateTime utc)
    {
        var date = DateOnly.FromDateTime(utc);
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    public static DateTime ToUtcMidnight(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}

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

    /// <summary>Monday of the week containing the given date.</summary>
    public static DateOnly ToWeekStart(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static DateTime ToUtcMidnight(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}

/// <summary>
/// The whole Monday–Sunday weeks spanned by a set of entry dates.
///
/// Weekly overtime is assessed per calendar week across every project, so cost
/// calculation has to widen a project's own date range out to week boundaries before
/// pulling the user's other entries. Shared by ProjectCostService and ReportService,
/// which previously derived the same window separately.
/// </summary>
public readonly record struct WeekWindow(DateOnly FirstWeekStart, DateOnly LastWeekEnd)
{
    public DateTime StartUtc => TimesheetWeek.ToUtcMidnight(FirstWeekStart);

    /// <summary>Exclusive upper bound — midnight after the last Sunday.</summary>
    public DateTime EndExclusiveUtc => TimesheetWeek.ToUtcMidnight(LastWeekEnd.AddDays(1));

    public bool Contains(DateTime instantUtc) =>
        instantUtc >= StartUtc && instantUtc < EndExclusiveUtc;

    /// <summary>Null when <paramref name="dates"/> is empty — there is no window to cover.</summary>
    public static WeekWindow? Covering(IEnumerable<DateOnly> dates)
    {
        DateOnly? min = null;
        DateOnly? max = null;

        foreach (var date in dates)
        {
            if (min is null || date < min) min = date;
            if (max is null || date > max) max = date;
        }

        if (min is null || max is null)
            return null;

        return new WeekWindow(
            TimesheetWeek.ToWeekStart(min.Value),
            TimesheetWeek.ToWeekStart(max.Value).AddDays(6));
    }
}

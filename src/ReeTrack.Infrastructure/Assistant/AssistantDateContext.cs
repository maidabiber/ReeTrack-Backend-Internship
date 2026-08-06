using System.Globalization;
using System.Text;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Assistant;

/// <summary>
/// Concrete Monday–Sunday calendar anchors for the time-entry assistant prompt,
/// so the model copies dates instead of inventing week boundaries.
/// </summary>
public static class AssistantDateContext
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly DayOfWeek[] AllDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday,
    ];

    private static readonly DayOfWeek[] WeekdayDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    ];

    public static DateOnly ResolveReferenceDate(string? referenceDate)
    {
        if (!string.IsNullOrWhiteSpace(referenceDate)
            && DateOnly.TryParseExact(
                referenceDate.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        return DateOnly.FromDateTime(DateTime.Today);
    }

    public static string BuildPromptBlock(
        DateOnly today,
        string? timeZone,
        string? referenceDateTime)
    {
        var thisWeekStart = TimesheetWeek.ToWeekStart(today);
        var thisWeekEnd = thisWeekStart.AddDays(6);
        var nextWeekStart = thisWeekStart.AddDays(7);
        var nextWeekEnd = nextWeekStart.AddDays(6);
        var lastWeekStart = thisWeekStart.AddDays(-7);
        var lastWeekEnd = lastWeekStart.AddDays(6);

        var sb = new StringBuilder();
        sb.AppendLine($"Today: {FormatDay(today)}.");
        if (!string.IsNullOrWhiteSpace(timeZone))
            sb.AppendLine($"User timezone (IANA): {timeZone.Trim()}.");
        if (!string.IsNullOrWhiteSpace(referenceDateTime))
            sb.AppendLine($"Local date-time right now: {referenceDateTime.Trim()} (wall-clock in the user's timezone — not UTC).");

        sb.AppendLine("Weeks in ReeTrack are Monday–Sunday (NOT Sunday–Saturday). Copy dates from this calendar; do not invent week boundaries.");
        sb.AppendLine($"This week: {FormatDay(thisWeekStart)} … {FormatDay(thisWeekEnd)}");
        sb.AppendLine($"  Days: {FormatWeekDays(thisWeekStart)}");
        sb.AppendLine($"Next week: {FormatDay(nextWeekStart)} … {FormatDay(nextWeekEnd)}");
        sb.AppendLine($"  Days: {FormatWeekDays(nextWeekStart)}");
        sb.AppendLine($"Last week: {FormatDay(lastWeekStart)} … {FormatDay(lastWeekEnd)}");
        sb.AppendLine($"  Days: {FormatWeekDays(lastWeekStart)}");

        sb.AppendLine("Preset entryDate lists (use SubmitWeekTimeEntryDraft — do not invent dates):");
        sb.AppendLine($"  every weekday this week → expandWeek=this, expandDays=weekdays → {FormatIsoList(ResolveWeekDates(today, "this", "weekdays"))}");
        sb.AppendLine($"  every weekday next week → expandWeek=next, expandDays=weekdays → {FormatIsoList(ResolveWeekDates(today, "next", "weekdays"))}");
        sb.AppendLine($"  every day next week (incl. weekend) → expandWeek=next, expandDays=all → {FormatIsoList(ResolveWeekDates(today, "next", "all"))}");
        sb.AppendLine($"  every weekday last week → expandWeek=last, expandDays=weekdays → {FormatIsoList(ResolveWeekDates(today, "last", "weekdays"))}");

        sb.AppendLine("Next occurrence of each weekday on or after today:");
        foreach (var day in AllDays)
            sb.AppendLine($"  Next {day}: {FormatIso(NextOnOrAfter(today, day))}");

        sb.AppendLine("Most recent past occurrence of each weekday (strictly before today):");
        foreach (var day in AllDays)
            sb.AppendLine($"  Last {day}: {FormatIso(MostRecentPast(today, day))}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Resolves concrete local dates for a named week. Returns null when week/days are invalid.
    /// </summary>
    public static IReadOnlyList<DateOnly>? ResolveWeekDates(DateOnly today, string? week, string? days)
    {
        if (string.IsNullOrWhiteSpace(week))
            return null;

        var weekStart = TimesheetWeek.ToWeekStart(today);
        weekStart = week.Trim().ToLowerInvariant() switch
        {
            "this" or "this_week" or "this-week" => weekStart,
            "next" or "next_week" or "next-week" => weekStart.AddDays(7),
            "last" or "last_week" or "last-week" or "previous" or "previous_week" => weekStart.AddDays(-7),
            _ => DateOnly.MinValue,
        };

        if (weekStart == DateOnly.MinValue)
            return null;

        var dayFilter = ParseDayFilter(days);
        if (dayFilter is null)
            return null;

        return dayFilter
            .Select(d => weekStart.AddDays(((int)d - (int)DayOfWeek.Monday + 7) % 7))
            .OrderBy(d => d)
            .ToList();
    }

    public static DateOnly NextOnOrAfter(DateOnly today, DayOfWeek dayOfWeek)
    {
        var delta = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(delta);
    }

    public static DateOnly MostRecentPast(DateOnly today, DayOfWeek dayOfWeek)
    {
        var delta = ((int)today.DayOfWeek - (int)dayOfWeek + 7) % 7;
        if (delta == 0)
            delta = 7;
        return today.AddDays(-delta);
    }

    private static IReadOnlyList<DayOfWeek>? ParseDayFilter(string? days)
    {
        if (string.IsNullOrWhiteSpace(days)
            || days.Equals("weekdays", StringComparison.OrdinalIgnoreCase)
            || days.Equals("weekday", StringComparison.OrdinalIgnoreCase)
            || days.Equals("mon-fri", StringComparison.OrdinalIgnoreCase)
            || days.Equals("monday-friday", StringComparison.OrdinalIgnoreCase))
        {
            return WeekdayDays;
        }

        if (days.Equals("all", StringComparison.OrdinalIgnoreCase)
            || days.Equals("everyday", StringComparison.OrdinalIgnoreCase)
            || days.Equals("every-day", StringComparison.OrdinalIgnoreCase)
            || days.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            return AllDays;
        }

        var parsed = new List<DayOfWeek>();
        foreach (var part in days.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseDayOfWeek(part, out var day))
                return null;
            if (!parsed.Contains(day))
                parsed.Add(day);
        }

        return parsed.Count > 0 ? parsed : null;
    }

    private static bool TryParseDayOfWeek(string value, out DayOfWeek day)
    {
        day = default;
        var key = value.Trim().ToLowerInvariant();
        day = key switch
        {
            "mon" or "monday" => DayOfWeek.Monday,
            "tue" or "tues" or "tuesday" => DayOfWeek.Tuesday,
            "wed" or "wednesday" => DayOfWeek.Wednesday,
            "thu" or "thur" or "thurs" or "thursday" => DayOfWeek.Thursday,
            "fri" or "friday" => DayOfWeek.Friday,
            "sat" or "saturday" => DayOfWeek.Saturday,
            "sun" or "sunday" => DayOfWeek.Sunday,
            _ => (DayOfWeek)(-1),
        };
        return (int)day >= 0;
    }

    private static string FormatWeekDays(DateOnly monday) =>
        string.Join(", ", Enumerable.Range(0, 7).Select(i => FormatDay(monday.AddDays(i))));

    private static string FormatIsoList(IReadOnlyList<DateOnly>? dates) =>
        dates is null || dates.Count == 0
            ? "(none)"
            : string.Join(", ", dates.Select(FormatIso));

    private static string FormatDay(DateOnly date) =>
        $"{date.ToString("dddd", En)} {FormatIso(date)}";

    private static string FormatIso(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Reports;

/// <summary>
/// Pure in-memory helpers for summary-report shaping. Kept separate from
/// <see cref="ReportService"/> so weekly-trend zero-fill can be unit-tested
/// without a database.
/// </summary>
public static class ReportAggregations
{
    public const int WeeklyTrendWeeks = 26;

    private static readonly DayOfWeek[] DaysMondayToSunday =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    public static IReadOnlyList<DayOfWeekHoursDto> BuildActivity(
        IEnumerable<(DayOfWeek Day, long Seconds)> secondsByDay)
    {
        var lookup = secondsByDay
            .GroupBy(x => x.Day)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Seconds));

        return DaysMondayToSunday
            .Select(day => new DayOfWeekHoursDto
            {
                DayOfWeek = day.ToString(),
                TotalSeconds = lookup.GetValueOrDefault(day)
            })
            .ToList();
    }

    /// <summary>
    /// Zero-filled weekly totals for the most recent <paramref name="weekCount"/>
    /// weeks ending at <paramref name="currentWeek"/> (inclusive), oldest first.
    /// </summary>
    public static IReadOnlyList<TrendPointDto> BuildWeeklyTrend(
        IEnumerable<(DateTime StartedAtUtc, long DurationSeconds)> entries,
        DateOnly currentWeek,
        int weekCount = WeeklyTrendWeeks)
    {
        if (weekCount < 1)
            throw new ArgumentOutOfRangeException(nameof(weekCount));

        var oldestWeek = currentWeek.AddDays(-7 * (weekCount - 1));
        var byWeek = entries
            .Select(e => (Week: TimesheetWeek.ToWeekStart(e.StartedAtUtc), e.DurationSeconds))
            .Where(e => e.Week >= oldestWeek && e.Week <= currentWeek)
            .GroupBy(e => e.Week)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.DurationSeconds));

        return Enumerable.Range(0, weekCount)
            .Select(i =>
            {
                var week = oldestWeek.AddDays(7 * i);
                return new TrendPointDto
                {
                    WeekStartDate = week,
                    TotalSeconds = byWeek.GetValueOrDefault(week)
                };
            })
            .ToList();
    }

    public static decimal BillablePct(long billableSeconds, long totalSeconds) =>
        totalSeconds <= 0
            ? 0m
            : Math.Round(billableSeconds * 100m / totalSeconds, 2, MidpointRounding.AwayFromZero);
}

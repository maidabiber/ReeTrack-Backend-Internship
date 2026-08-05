using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Infrastructure.Reports.Custom;

/// <summary>
/// Resolves the baseline window a report is measured against.
/// </summary>
internal static class ComparisonWindow
{
    /// <summary>
    /// Builds the baseline query by shifting the report's own range back, keeping every other
    /// filter identical so the two windows differ only by date.
    /// </summary>
    /// <remarks>
    /// Requires an explicit From and To. An open-ended range has no defined length, and
    /// inventing one (say, "the period before the first entry") would silently compare against
    /// a window the user never asked for.
    /// </remarks>
    public static bool TryResolve(ReportQuery query, ComparisonMode mode, out ReportQuery baseline)
    {
        baseline = query;

        if (mode == ComparisonMode.None || query.From is not { } from || query.To is not { } to || from > to)
            return false;

        var (baselineFrom, baselineTo) = mode switch
        {
            ComparisonMode.PreviousPeriod => Preceding(from, to),
            ComparisonMode.SamePeriodLastYear => (AYearBefore(from), AYearBefore(to)),
            _ => (from, to)
        };

        baseline = new ReportQuery
        {
            UserIds = query.UserIds,
            ProjectIds = query.ProjectIds,
            ClientIds = query.ClientIds,
            TaskIds = query.TaskIds,
            TagIds = query.TagIds,
            Billable = query.Billable,
            From = baselineFrom,
            To = baselineTo,
            GroupBy = query.GroupBy
        };

        return true;
    }

    /// <summary>The equal-length window ending the day before the report starts.</summary>
    private static (DateOnly From, DateOnly To) Preceding(DateOnly from, DateOnly to)
    {
        var lengthInDays = to.DayNumber - from.DayNumber + 1;
        var baselineTo = from.AddDays(-1);
        return (baselineTo.AddDays(-(lengthInDays - 1)), baselineTo);
    }

    /// <summary>
    /// 29 February has no counterpart in a common year, so it lands on 28 February rather than
    /// throwing or rolling into March.
    /// </summary>
    private static DateOnly AYearBefore(DateOnly date)
    {
        var year = date.Year - 1;
        var day = Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month));
        return new DateOnly(year, date.Month, day);
    }
}

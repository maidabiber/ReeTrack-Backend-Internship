using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Reports;

public static class ReportQueryRules
{
    private const int MaxValuesPerDimension = 200;

    /// <summary>
    /// Widest explicit date range a report query can request. `ReportEntryPipeline`
    /// materialises every matching entry with no other bound — an unfiltered multi-year
    /// range on a large team can pull the whole time-entry table into memory. A fully
    /// open range (both From and To unset — the "All time" report) is left alone here;
    /// bounding *that* safely is a bigger, product-facing question (a sensible default
    /// cutoff, or server-side pagination) tracked separately rather than solved by
    /// silently truncating an intentionally-supported feature.
    /// </summary>
    private const int MaxExplicitRangeDays = 400;

    public static ReportQuery NormalizeAndValidate(ReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From is { } from && query.To is { } to && from > to)
            throw AppErrors.Validation("The report start date must be on or before the end date.");

        if (query.From is { } rangeFrom && query.To is { } rangeTo
            && rangeTo.DayNumber - rangeFrom.DayNumber > MaxExplicitRangeDays)
        {
            throw AppErrors.Validation(
                $"A report can cover at most {MaxExplicitRangeDays} days — narrow the date range.");
        }

        if (query.GroupBy.Any(group => !Enum.IsDefined(group)))
            throw AppErrors.Validation("The report contains an unsupported grouping.");

        return new ReportQuery
        {
            UserIds = NormalizeIds(query.UserIds, "users"),
            ProjectIds = NormalizeIds(query.ProjectIds, "projects"),
            ClientIds = NormalizeIds(query.ClientIds, "clients"),
            TaskIds = NormalizeIds(query.TaskIds, "tasks"),
            TagIds = NormalizeIds(query.TagIds, "tags"),
            Billable = query.Billable,
            From = query.From,
            To = query.To,
            GroupBy = query.GroupBy.Distinct().ToList()
        };
    }

    private static IReadOnlyList<Guid> NormalizeIds(IEnumerable<Guid> ids, string dimension)
    {
        var normalized = ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalized.Count > MaxValuesPerDimension)
        {
            throw AppErrors.Validation(
                $"A report can filter by at most {MaxValuesPerDimension} {dimension}.");
        }

        return normalized;
    }
}

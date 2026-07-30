using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Reports;

/// <summary>
/// Sorts detailed entries and builds contiguous group sections from ReportQuery.GroupBy.
/// </summary>
internal static class DetailedReportGrouping
{
    public static IReadOnlyList<DetailedEntryDto> Sort(
        IReadOnlyList<DetailedEntryDto> entries,
        IReadOnlyList<ReportGroupBy> groupBy)
    {
        if (entries.Count == 0)
            return entries;

        IOrderedEnumerable<DetailedEntryDto>? ordered = null;

        foreach (var dimension in groupBy)
        {
            ordered = ordered is null
                ? entries.OrderBy(e => GroupKey(e, dimension), StringComparer.OrdinalIgnoreCase)
                : ordered.ThenBy(e => GroupKey(e, dimension), StringComparer.OrdinalIgnoreCase);
        }

        ordered = ordered is null
            ? entries.OrderBy(e => e.EntryDate).ThenBy(e => e.StartedAtUtc ?? DateTime.MinValue)
            : ordered.ThenBy(e => e.EntryDate).ThenBy(e => e.StartedAtUtc ?? DateTime.MinValue);

        return ordered.ThenBy(e => e.EntryId).ToList();
    }

    public static IReadOnlyList<DetailedGroupDto> BuildGroups(
        IReadOnlyList<DetailedEntryDto> sortedEntries,
        IReadOnlyList<ReportGroupBy> groupBy)
    {
        if (groupBy.Count == 0 || sortedEntries.Count == 0)
            return [];

        var groups = new List<DetailedGroupDto>();
        var start = 0;

        while (start < sortedEntries.Count)
        {
            var keys = KeysFor(sortedEntries[start], groupBy);
            var end = start + 1;
            while (end < sortedEntries.Count
                   && KeysEqual(keys, KeysFor(sortedEntries[end], groupBy)))
            {
                end++;
            }

            var slice = sortedEntries.Skip(start).Take(end - start).ToList();
            groups.Add(new DetailedGroupDto
            {
                Label = string.Join(" / ", keys),
                Keys = keys,
                TotalSeconds = slice.Sum(e => e.DurationSeconds),
                CalculatedCost = slice.Sum(e => e.CalculatedCost),
                EntryCount = slice.Count,
                StartIndex = start,
                EndIndexExclusive = end
            });
            start = end;
        }

        return groups;
    }

    private static IReadOnlyList<string> KeysFor(
        DetailedEntryDto entry,
        IReadOnlyList<ReportGroupBy> groupBy) =>
        groupBy.Select(dimension => GroupKey(entry, dimension)).ToList();

    private static bool KeysEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string GroupKey(DetailedEntryDto entry, ReportGroupBy dimension) =>
        dimension switch
        {
            ReportGroupBy.User => entry.DisplayName,
            ReportGroupBy.Project => entry.ProjectName ?? "(Unassigned)",
            ReportGroupBy.Client => entry.ClientName ?? "(No client)",
            ReportGroupBy.Task => entry.TaskName ?? "(No task)",
            ReportGroupBy.Tag => entry.Tags.Count == 0
                ? "(No tags)"
                : string.Join(", ", entry.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase)),
            ReportGroupBy.Billable => entry.IsBillable ? "Billable" : "Non-billable",
            ReportGroupBy.Day => entry.EntryDate.ToString("yyyy-MM-dd"),
            ReportGroupBy.Week => TimesheetWeek.ToWeekStart(entry.EntryDate).ToString("yyyy-MM-dd"),
            _ => "(Unknown)"
        };
}

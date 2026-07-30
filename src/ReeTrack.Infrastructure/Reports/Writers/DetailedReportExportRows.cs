using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

/// <summary>
/// Shared helpers for rendering detailed export rows with optional group sections.
/// Index ranges on <see cref="DetailedGroupDto"/> refer to the full sorted entry list.
/// </summary>
internal static class DetailedReportExportRows
{
    /// <summary>
    /// Same summary shape as the detailed report page group header:
    /// "{label} · {n} entries · {hours}".
    /// </summary>
    public static string GroupSummary(DetailedGroupDto group) =>
        $"{group.Label} · {group.EntryCount} entries · {ReportFormat.HoursLabel(group.TotalSeconds)}";

    /// <summary>
    /// Yields each group with its contiguous entry slice, or a single ungrouped batch
    /// when <see cref="DetailedReportDto.Groups"/> is empty.
    /// </summary>
    public static IEnumerable<(DetailedGroupDto? Group, IReadOnlyList<DetailedEntryDto> Entries)> Enumerate(
        DetailedReportDto model)
    {
        if (model.Groups.Count == 0)
        {
            yield return (null, model.Entries);
            yield break;
        }

        foreach (var group in model.Groups)
        {
            var count = Math.Max(0, group.EndIndexExclusive - group.StartIndex);
            // Indexed slice, not Skip/Take: Skip re-walks from the front of the sequence
            // on every call (it doesn't know model.Entries is a List), so this was O(n²)
            // across all groups for a report with many small groups.
            var slice = new List<DetailedEntryDto>(count);
            for (var i = group.StartIndex; i < group.StartIndex + count; i++)
                slice.Add(model.Entries[i]);
            yield return (group, slice);
        }
    }
}

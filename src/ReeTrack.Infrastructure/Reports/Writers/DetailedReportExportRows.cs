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
            var slice = model.Entries
                .Skip(group.StartIndex)
                .Take(count)
                .ToList();
            yield return (group, slice);
        }
    }
}

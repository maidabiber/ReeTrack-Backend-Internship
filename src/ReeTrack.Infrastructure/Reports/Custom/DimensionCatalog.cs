using System.Globalization;

namespace ReeTrack.Infrastructure.Reports.Custom;

internal readonly record struct DimensionKey(string Value, string Label, long SortHint);

internal sealed record DimensionDefinition(
    string Id,
    string Label,
    bool FansOut,
    Func<EntryRow, IReadOnlyList<DimensionKey>> KeysOf,
    /// <summary>Explains the double counting when <see cref="FansOut"/>; shown as a block footnote.</summary>
    string? FanOutNote = null);

internal static class DimensionCatalog
{
    public const string TagFanOutFootnote =
        "Entries with several tags are counted under each — rows can total more than the period.";

    public const string HourTypeFanOutFootnote =
        "An entry counts under every hour type it qualifies for (weekend time can also be "
        + "overtime), so rows can total more than the period.";

    public static IReadOnlyDictionary<string, DimensionDefinition> All { get; } =
        new Dictionary<string, DimensionDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["user"] = new("user", "Member", FansOut: false, row =>
            [
                new DimensionKey(row.UserId.ToString(), row.UserName, NamedSortHint)
            ]),
            ["project"] = new("project", "Project", FansOut: false, row =>
            [
                new DimensionKey(
                    row.ProjectId?.ToString() ?? "unassigned",
                    row.ProjectLabel,
                    row.ProjectId is null ? UnassignedSortHint : NamedSortHint)
            ]),
            ["client"] = new("client", "Client", FansOut: false, row =>
            [
                new DimensionKey(
                    row.ClientId?.ToString() ?? "unassigned",
                    row.ClientLabel,
                    row.ClientId is null ? UnassignedSortHint : NamedSortHint)
            ]),
            ["task"] = new("task", "Task", FansOut: false, row =>
            [
                new DimensionKey(
                    row.TaskId?.ToString() ?? "none",
                    row.TaskLabel,
                    row.TaskId is null ? UnassignedSortHint : NamedSortHint)
            ]),
            ["tag"] = new("tag", "Tag", FansOut: true, FanOutNote: TagFanOutFootnote, KeysOf: row =>
            {
                if (row.Tags.Count == 0)
                    return [new DimensionKey("none", "(No tags)", UnassignedSortHint)];

                return row.Tags
                    .Select(tag => new DimensionKey(
                        tag.Id.ToString(),
                        tag.Label,
                        NamedSortHint))
                    .ToList();
            }),
            ["day"] = new("day", "Day", FansOut: false, row =>
            [
                new DimensionKey(
                    row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    row.Date.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
                    row.Date.DayNumber)
            ]),
            ["dayOfWeek"] = new("dayOfWeek", "Day of week", FansOut: false, row =>
            {
                var order = row.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)row.Date.DayOfWeek;
                return
                [
                    new DimensionKey(
                        row.Date.DayOfWeek.ToString(),
                        row.Date.DayOfWeek.ToString(),
                        order)
                ];
            }),
            ["week"] = new("week", "Week", FansOut: false, row =>
            [
                new DimensionKey(
                    row.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    $"Week of {row.WeekStart.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}",
                    row.WeekStart.DayNumber)
            ]),
            ["month"] = new("month", "Month", FansOut: false, row =>
            {
                var monthStart = new DateOnly(row.Date.Year, row.Date.Month, 1);
                return
                [
                    new DimensionKey(
                        monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                        monthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                        monthStart.DayNumber)
                ];
            }),
            ["billable"] = new("billable", "Billable", FansOut: false, row =>
            [
                new DimensionKey(
                    row.IsBillable ? "billable" : "nonBillable",
                    row.IsBillable ? "Billable" : "Non-billable",
                    row.IsBillable ? 0 : 1)
            ]),
            // Fans out. The hour buckets on a cost line overlap — a Saturday shift that runs
            // past the weekly threshold carries both weekend hours and overtime hours — so
            // forcing one bucket per entry made this dimension contradict the weekendHours /
            // overtimeHours metrics derived from the same cost line.
            ["hourType"] = new(
                "hourType",
                "Hour type",
                FansOut: true,
                ResolveHourTypes,
                FanOutNote: HourTypeFanOutFootnote),
        };

    public static DimensionDefinition GetRequired(string id) =>
        All.TryGetValue(id, out var definition)
            ? definition
            : throw Application.Common.Exceptions.AppErrors.Validation(
                $"Unknown dimension '{id}'.");

    /// <summary>
    /// Every bucket an entry qualifies for, so each row's weekendHours / holidayHours /
    /// overtimeHours agree with the same metrics measured anywhere else. An entry can appear
    /// under more than one type, which is what <see cref="DimensionDefinition.FansOut"/>
    /// warns about — totalHours on this dimension double counts exactly like it does on tags.
    /// </summary>
    private static IReadOnlyList<DimensionKey> ResolveHourTypes(EntryRow row)
    {
        var isWeekend = row.Cost?.IsWeekend
            ?? row.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var isHoliday = row.Cost?.IsHoliday == true;
        var overtimeHours = row.Cost?.OvertimeHours ?? 0m;
        var totalHours = row.Cost?.TotalHours ?? 0m;

        var keys = new List<DimensionKey>(3);
        if (isHoliday)
            keys.Add(HourTypeKey("Holiday", 0));
        if (isWeekend)
            keys.Add(HourTypeKey("Weekend", 1));
        if (overtimeHours > 0m)
            keys.Add(HourTypeKey("Overtime", 2));

        // Whatever is left over is ordinary time. Weekend and holiday entries are premium in
        // full, so only a weekday entry can carry both overtime and normal hours.
        var hasPlainTime = !isWeekend && !isHoliday && overtimeHours < totalHours;
        if (hasPlainTime || keys.Count == 0)
            keys.Add(HourTypeKey("Normal", 3));

        return keys;
    }

    private static DimensionKey HourTypeKey(string type, long sortHint) =>
        new(type, type, sortHint);

    /// <summary>
    /// Entity dimensions carry no natural order, so every key shares a hint and callers fall
    /// through to the alphabetical tie-break. (Hashing the id instead produced a stable but
    /// arbitrary order that looked random to users.)
    /// </summary>
    private const long NamedSortHint = 0L;

    /// <summary>Buckets with no entity ("Unassigned", "(No tags)") sort last.</summary>
    private const long UnassignedSortHint = long.MaxValue;
}

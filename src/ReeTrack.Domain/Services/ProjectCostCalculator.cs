using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Services;

public sealed class ProjectCostCalculator : IProjectCostCalculator
{
    private readonly IReadOnlyList<IRateMultiplier> _multipliers;

    public ProjectCostCalculator(IEnumerable<IRateMultiplier> multipliers)
    {
        _multipliers = multipliers
            .OrderBy(m => m.ExecutionOrder)
            .ToList();
    }

    public ProjectCostResult Calculate(
        Project project,
        IReadOnlyList<TimeEntry> projectEntries,
        IReadOnlyList<TimeEntry> crossProjectUserEntries,
        IReadOnlyList<UserHourlyRate> userRates,
        IReadOnlySet<DateOnly> holidays,
        RateMultiplierConfig multiplierConfig)
    {
        var projectRate = project.HourlyRate ?? 0m;
        var lines = CalculateEntryLines(
            projectEntries,
            crossProjectUserEntries,
            userRates,
            holidays,
            multiplierConfig,
            _ => projectRate);

        decimal total = 0m;
        decimal totalHours = 0m;
        decimal weekendHours = 0m;
        decimal holidayHours = 0m;
        decimal overtimeHours = 0m;
        decimal normalCost = 0m;
        decimal weekendCost = 0m;
        decimal holidayCost = 0m;
        decimal overtimeCost = 0m;
        var taskTotals = new Dictionary<Guid, TaskAccumulator>();

        var lineById = lines.ToDictionary(line => line.EntryId);
        foreach (var entry in projectEntries)
        {
            if (!lineById.TryGetValue(entry.Id, out var line))
                continue;

            total += line.CalculatedCost;
            totalHours += line.TotalHours;
            weekendHours += line.WeekendHours;
            holidayHours += line.HolidayHours;
            overtimeHours += line.OvertimeHours;
            normalCost += line.NormalCost;
            weekendCost += line.WeekendCost;
            holidayCost += line.HolidayCost;
            overtimeCost += line.OvertimeCost;

            if (entry.ProjectTaskId is Guid taskId)
            {
                if (!taskTotals.TryGetValue(taskId, out var task))
                {
                    task = new TaskAccumulator();
                    taskTotals[taskId] = task;
                }

                task.CalculatedCost += line.CalculatedCost;
                task.TotalHours += line.TotalHours;
                task.WeekendHours += line.WeekendHours;
                task.HolidayHours += line.HolidayHours;
                task.OvertimeHours += line.OvertimeHours;
            }
        }

        var taskCosts = taskTotals
            .OrderBy(pair => pair.Key)
            .Select(pair => new ProjectTaskCostResult(
                pair.Key,
                Math.Round(pair.Value.CalculatedCost, 2, MidpointRounding.AwayFromZero),
                Math.Round(pair.Value.TotalHours, 2, MidpointRounding.AwayFromZero),
                Math.Round(pair.Value.WeekendHours, 2, MidpointRounding.AwayFromZero),
                Math.Round(pair.Value.HolidayHours, 2, MidpointRounding.AwayFromZero),
                Math.Round(pair.Value.OvertimeHours, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return new ProjectCostResult(
            Math.Round(total, 2, MidpointRounding.AwayFromZero),
            Math.Round(totalHours, 2, MidpointRounding.AwayFromZero),
            Math.Round(weekendHours, 2, MidpointRounding.AwayFromZero),
            Math.Round(holidayHours, 2, MidpointRounding.AwayFromZero),
            Math.Round(overtimeHours, 2, MidpointRounding.AwayFromZero),
            Math.Round(normalCost, 2, MidpointRounding.AwayFromZero),
            Math.Round(weekendCost, 2, MidpointRounding.AwayFromZero),
            Math.Round(holidayCost, 2, MidpointRounding.AwayFromZero),
            Math.Round(overtimeCost, 2, MidpointRounding.AwayFromZero),
            taskCosts);
    }

    public IReadOnlyList<EntryCostLine> CalculateEntries(
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<TimeEntry> crossProjectUserEntries,
        IReadOnlyList<UserHourlyRate> userRates,
        IReadOnlySet<DateOnly> holidays,
        RateMultiplierConfig multiplierConfig) =>
        CalculateEntryLines(
            entries,
            crossProjectUserEntries,
            userRates,
            holidays,
            multiplierConfig,
            entry => entry.Project?.HourlyRate ?? 0m);

    private IReadOnlyList<EntryCostLine> CalculateEntryLines(
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<TimeEntry> crossProjectUserEntries,
        IReadOnlyList<UserHourlyRate> userRates,
        IReadOnlySet<DateOnly> holidays,
        RateMultiplierConfig multiplierConfig,
        Func<TimeEntry, decimal> projectRateFor)
    {
        var cumulativeWeeklyHours = CalculateCumulativeWeeklyHours(crossProjectUserEntries);
        var lines = new List<EntryCostLine>(entries.Count);

        foreach (var entry in entries)
        {
            if (entry.Status != TimeEntryStatus.Confirmed)
                continue;

            if (entry.DeletedAtUtc is not null)
                continue;

            var entryDate = ResolveEntryDate(entry);
            var entryHours = entry.DurationSeconds / 3600m;
            var hoursBeforeEntry = cumulativeWeeklyHours.GetValueOrDefault(entry.Id);
            var isHoliday = holidays.Contains(entryDate);
            var isWeekend = entryDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var entryWeekendHours = isWeekend ? entryHours : 0m;
            var entryHolidayHours = isHoliday ? entryHours : 0m;
            var entryOvertimeHours = CalculateOvertimeHours(
                entryHours,
                hoursBeforeEntry,
                multiplierConfig.WeeklyOvertimeThresholdHours);

            var userRate = ResolveUserRate(userRates, entry.UserId, entryDate);
            var baseRate = Math.Max(userRate, projectRateFor(entry));

            var context = new RateContext(
                entry,
                entryDate,
                baseRate,
                hoursBeforeEntry,
                isHoliday,
                multiplierConfig);
            var appliedRate = baseRate;
            foreach (var multiplier in _multipliers)
                appliedRate = multiplier.Apply(appliedRate, context);

            var entryCost = entryHours * appliedRate;
            decimal normalCost = 0m;
            decimal weekendCost = 0m;
            decimal holidayCost = 0m;
            decimal overtimeCost = 0m;
            AttributeEntryCost(
                entryCost,
                entryHours,
                entryOvertimeHours,
                isWeekend,
                isHoliday,
                ref normalCost,
                ref weekendCost,
                ref holidayCost,
                ref overtimeCost);

            lines.Add(new EntryCostLine(
                entry.Id,
                Math.Round(entryCost, 2, MidpointRounding.AwayFromZero),
                Math.Round(normalCost, 2, MidpointRounding.AwayFromZero),
                Math.Round(weekendCost, 2, MidpointRounding.AwayFromZero),
                Math.Round(holidayCost, 2, MidpointRounding.AwayFromZero),
                Math.Round(overtimeCost, 2, MidpointRounding.AwayFromZero),
                Math.Round(entryHours, 4, MidpointRounding.AwayFromZero),
                Math.Round(entryWeekendHours, 4, MidpointRounding.AwayFromZero),
                Math.Round(entryHolidayHours, 4, MidpointRounding.AwayFromZero),
                Math.Round(entryOvertimeHours, 4, MidpointRounding.AwayFromZero),
                isWeekend,
                isHoliday));
        }

        return lines;
    }

    /// <summary>
    /// Mutually exclusive cost buckets that sum to entry cost:
    /// Weekend (any Sat/Sun) → WeekendCost;
    /// else weekday holiday → HolidayCost;
    /// else split weekday cost by OT hour share → NormalCost / OvertimeCost.
    /// </summary>
    private static void AttributeEntryCost(
        decimal entryCost,
        decimal entryHours,
        decimal entryOvertimeHours,
        bool isWeekend,
        bool isHoliday,
        ref decimal normalCost,
        ref decimal weekendCost,
        ref decimal holidayCost,
        ref decimal overtimeCost)
    {
        if (isWeekend)
        {
            weekendCost += entryCost;
            return;
        }

        if (isHoliday)
        {
            holidayCost += entryCost;
            return;
        }

        if (entryHours > 0m && entryOvertimeHours > 0m)
        {
            var otRatio = Math.Clamp(entryOvertimeHours / entryHours, 0m, 1m);
            overtimeCost += entryCost * otRatio;
            normalCost += entryCost * (1m - otRatio);
            return;
        }

        normalCost += entryCost;
    }

    private static decimal CalculateOvertimeHours(
        decimal entryHours,
        decimal hoursBeforeEntry,
        decimal weeklyThresholdHours)
    {
        if (entryHours <= 0m)
            return 0m;

        var regularHours = Math.Clamp(
            weeklyThresholdHours - hoursBeforeEntry,
            0m,
            entryHours);
        return entryHours - regularHours;
    }

    private static IReadOnlyDictionary<Guid, decimal> CalculateCumulativeWeeklyHours(
        IReadOnlyList<TimeEntry> entries)
    {
        var cumulativeHoursByEntryId = new Dictionary<Guid, decimal>();

        foreach (var userWeekEntries in entries
                     .Where(IsConfirmedAndActive)
                     .GroupBy(entry => new { entry.UserId, WeekStart = GetWeekStart(ResolveEntryDate(entry)) }))
        {
            decimal cumulativeHours = 0m;
            foreach (var entry in userWeekEntries.OrderBy(entry => entry.StartedAtUtc ?? entry.CreatedAtUtc))
            {
                cumulativeHoursByEntryId[entry.Id] = cumulativeHours;
                cumulativeHours += entry.DurationSeconds / 3600m;
            }
        }

        return cumulativeHoursByEntryId;
    }

    private static bool IsConfirmedAndActive(TimeEntry entry) =>
        entry.Status == TimeEntryStatus.Confirmed && entry.DeletedAtUtc is null;

    private static DateOnly GetWeekStart(DateOnly date) =>
        date.AddDays(-((7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7));

    private static DateOnly ResolveEntryDate(TimeEntry entry)
    {
        var instant = entry.StartedAtUtc ?? entry.CreatedAtUtc;
        return DateOnly.FromDateTime(instant);
    }

    private static decimal ResolveUserRate(
        IReadOnlyList<UserHourlyRate> userRates,
        Guid userId,
        DateOnly entryDate)
    {
        var rate = userRates.FirstOrDefault(r => r.UserId == userId && r.Covers(entryDate));
        return rate?.Rate.Amount ?? 0m;
    }

    private sealed class TaskAccumulator
    {
        public decimal CalculatedCost { get; set; }
        public decimal TotalHours { get; set; }
        public decimal WeekendHours { get; set; }
        public decimal HolidayHours { get; set; }
        public decimal OvertimeHours { get; set; }
    }
}

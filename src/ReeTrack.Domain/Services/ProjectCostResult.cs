namespace ReeTrack.Domain.Services;

public sealed record ProjectTaskCostResult(
    Guid ProjectTaskId,
    decimal CalculatedCost,
    decimal TotalHours,
    decimal WeekendHours,
    decimal HolidayHours,
    decimal OvertimeHours);

/// <summary>
/// Per-entry labour cost using the same weekend / holiday / OT bucket rules as
/// <see cref="ProjectCostResult"/>. Used by the detailed (audit) report.
/// Values are unrounded — callers must round at their own presentation boundary
/// (e.g. when mapping to a DTO or summing across entries) rather than relying on
/// pre-rounded figures, which double-rounds and drifts from the true total.
/// </summary>
public sealed record EntryCostLine(
    Guid EntryId,
    decimal CalculatedCost,
    decimal NormalCost,
    decimal WeekendCost,
    decimal HolidayCost,
    decimal OvertimeCost,
    decimal TotalHours,
    decimal WeekendHours,
    decimal HolidayHours,
    decimal OvertimeHours,
    bool IsWeekend,
    bool IsHoliday);

public sealed record ProjectCostResult(
    decimal CalculatedCost,
    decimal TotalHours,
    decimal WeekendHours,
    decimal HolidayHours,
    decimal OvertimeHours,
    /// <summary>Weekday non-holiday regular (non-OT) entry cost. Mutually exclusive with other *Cost fields.</summary>
    decimal NormalCost,
    /// <summary>Full cost of weekend entries (Sat/Sun), including any stacked premiums.</summary>
    decimal WeekendCost,
    /// <summary>Full cost of weekday holiday entries.</summary>
    decimal HolidayCost,
    /// <summary>Overtime portion of weekday non-holiday entry cost.</summary>
    decimal OvertimeCost,
    IReadOnlyList<ProjectTaskCostResult> TaskCosts);

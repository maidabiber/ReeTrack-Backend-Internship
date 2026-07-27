namespace ReeTrack.Domain.Services;

public sealed record ProjectTaskCostResult(
    Guid ProjectTaskId,
    decimal CalculatedCost,
    decimal TotalHours,
    decimal WeekendHours,
    decimal HolidayHours,
    decimal OvertimeHours);

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

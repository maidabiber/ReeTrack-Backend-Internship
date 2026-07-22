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
    IReadOnlyList<ProjectTaskCostResult> TaskCosts);

namespace ReeTrack.Application.Common.Models;

public sealed class ProjectTaskCostDto
{
    public required Guid ProjectTaskId { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required decimal TotalHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }
    public required decimal OvertimeHours { get; init; }
}

public sealed class ProjectCostDto
{
    public required Guid ProjectId { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required decimal TotalHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }
    public required decimal OvertimeHours { get; init; }
    public required DateTime CalculatedAtUtc { get; init; }
    public required IReadOnlyList<ProjectTaskCostDto> TaskCosts { get; init; }
}

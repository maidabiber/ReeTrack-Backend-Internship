namespace ReeTrack.Api.Contracts;

public sealed class ProjectTaskCostResponse
{
    public required Guid ProjectTaskId { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required decimal TotalHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }
    public required decimal OvertimeHours { get; init; }
}

public sealed class ProjectCostResponse
{
    public required Guid ProjectId { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required decimal TotalHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }
    public required decimal OvertimeHours { get; init; }
    public required DateTime CalculatedAtUtc { get; init; }
    public required IReadOnlyList<ProjectTaskCostResponse> TaskCosts { get; init; }
}

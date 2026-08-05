namespace ReeTrack.Application.Common.Models;

public sealed class OverviewDigestDto
{
    public required IReadOnlyList<OverviewDailySecondsDto> Activity { get; init; }
    public required IReadOnlyList<OverviewWeeklyTrendDto> WeeklyTrend { get; init; }
    public required decimal OvertimeSeconds { get; init; }
    public required decimal WeekendSeconds { get; init; }
    public required decimal HolidaySeconds { get; init; }
    public required IReadOnlyList<OverviewProjectDigestDto> Projects { get; init; }
    public required IReadOnlyList<OverviewMemberDigestDto> Members { get; init; }
}

public sealed class OverviewDailySecondsDto
{
    public required string Day { get; init; }
    public required long Seconds { get; init; }
}

public sealed class OverviewWeeklyTrendDto
{
    public required string Week { get; init; }
    public required long Seconds { get; init; }
    public required string Status { get; init; }
}

public sealed class OverviewProjectDigestDto
{
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public string? Color { get; init; }
    public required long TotalSeconds { get; init; }
    public required decimal BillablePct { get; init; }
    public decimal? CalculatedCost { get; init; }
    public string? Currency { get; init; }
    public string? ClientName { get; init; }
    public string? Status { get; init; }
    public decimal? TimeEstimateHours { get; init; }
}

public sealed class OverviewMemberDigestDto
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required long TotalSeconds { get; init; }
}

namespace ReeTrack.Api.Contracts;

public sealed class SummaryReportResponse
{
    public required ReportKpisResponse Kpis { get; init; }
    public required IReadOnlyList<DayOfWeekHoursResponse> Activity { get; init; }
    public required IReadOnlyList<TrendPointResponse> WeeklyTrend { get; init; }
    public required IReadOnlyList<ProjectSummaryResponse> Projects { get; init; }
    public required IReadOnlyList<MemberHoursResponse> Members { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
}

public sealed class ReportKpisResponse
{
    public required long TotalSeconds { get; init; }
    public required long BillableSeconds { get; init; }
    public required long NonBillableSeconds { get; init; }
    public required decimal BillablePct { get; init; }
    public required int EntryCount { get; init; }
    public required int ActiveMembers { get; init; }
    public required int ActiveProjects { get; init; }
    public required decimal OvertimeHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }
}

public sealed class DayOfWeekHoursResponse
{
    public required string DayOfWeek { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class TrendPointResponse
{
    public required DateOnly WeekStartDate { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class ProjectSummaryResponse
{
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string CurrencyCode { get; init; }
    public required long TotalSeconds { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required decimal OvertimeHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }
}

public sealed class MemberHoursResponse
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required long TotalSeconds { get; init; }
}

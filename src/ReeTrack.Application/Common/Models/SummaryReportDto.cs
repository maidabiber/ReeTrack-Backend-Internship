namespace ReeTrack.Application.Common.Models;

public sealed class SummaryReportDto
{
    public required ReportKpisDto Kpis { get; init; }
    public required IReadOnlyList<DayOfWeekHoursDto> Activity { get; init; }
    public required IReadOnlyList<TrendPointDto> WeeklyTrend { get; init; }
    public required IReadOnlyList<ProjectSummaryDto> Projects { get; init; }
    public required IReadOnlyList<MemberHoursDto> Members { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
}

public sealed class ReportKpisDto
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

public sealed class DayOfWeekHoursDto
{
    /// <summary>Day name, e.g. "Monday" … "Sunday".</summary>
    public required string DayOfWeek { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class TrendPointDto
{
    public required DateOnly WeekStartDate { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class ProjectSummaryDto
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

public sealed class MemberHoursDto
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required long TotalSeconds { get; init; }
}

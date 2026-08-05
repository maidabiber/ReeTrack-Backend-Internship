namespace ReeTrack.Api.Contracts;

public sealed class AdminOverviewResponse
{
    public required DateTime GeneratedAtUtc { get; init; }
    public required string Scope { get; init; }
    public required OverviewTodayKpisResponse Today { get; init; }
    public required int OnTheClock { get; init; }
    public required IReadOnlyList<ActiveTimerOverviewResponse> ActiveTimers { get; init; }
    public required IReadOnlyList<IdleMemberOverviewResponse> IdleMembers { get; init; }
    public required int IdleCount { get; init; }
    public required IReadOnlyList<OverviewProjectHoursResponse> TopProjects { get; init; }
    public OverviewDigestResponse? Digest { get; init; }
}

public sealed class OverviewTodayKpisResponse
{
    public required DateOnly Date { get; init; }
    public required long TotalSeconds { get; init; }
    public required long BillableSeconds { get; init; }
    public required decimal BillablePct { get; init; }
    public required int EntryCount { get; init; }
    public required int MembersLogged { get; init; }
    public required long UnassignedSeconds { get; init; }
}

public sealed class ActiveTimerOverviewResponse
{
    public required Guid TimeEntryId { get; init; }
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public string? Description { get; init; }
    public required bool IsBillable { get; init; }
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public string? ProjectColor { get; init; }
    public Guid? ProjectTaskId { get; init; }
    public string? ProjectTaskName { get; init; }
    public required bool IsUnassigned { get; init; }
    public required bool IsStale { get; init; }
}

public sealed class IdleMemberOverviewResponse
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}

public sealed class OverviewProjectHoursResponse
{
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class OverviewDigestResponse
{
    public required IReadOnlyList<OverviewDailySecondsResponse> Activity { get; init; }
    public required IReadOnlyList<OverviewWeeklyTrendResponse> WeeklyTrend { get; init; }
    public required decimal OvertimeSeconds { get; init; }
    public required decimal WeekendSeconds { get; init; }
    public required decimal HolidaySeconds { get; init; }
    public required IReadOnlyList<OverviewProjectDigestResponse> Projects { get; init; }
    public required IReadOnlyList<OverviewMemberDigestResponse> Members { get; init; }
}

public sealed class OverviewDailySecondsResponse
{
    public required string Day { get; init; }
    public required long Seconds { get; init; }
}

public sealed class OverviewWeeklyTrendResponse
{
    public required string Week { get; init; }
    public required long Seconds { get; init; }
    public required string Status { get; init; }
}

public sealed class OverviewProjectDigestResponse
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

public sealed class OverviewMemberDigestResponse
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required long TotalSeconds { get; init; }
}

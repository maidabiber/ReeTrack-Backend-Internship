namespace ReeTrack.Application.Common.Models;

public sealed class AdminOverviewDto
{
    public required DateTime GeneratedAtUtc { get; init; }
    public required string Scope { get; init; }
    public required OverviewTodayKpisDto Today { get; init; }
    public required int OnTheClock { get; init; }
    public required IReadOnlyList<ActiveTimerOverviewDto> ActiveTimers { get; init; }
    public required IReadOnlyList<IdleMemberOverviewDto> IdleMembers { get; init; }
    public required int IdleCount { get; init; }
    public required IReadOnlyList<OverviewProjectHoursDto> TopProjects { get; init; }
    public OverviewDigestDto? Digest { get; init; }
}

public sealed class OverviewTodayKpisDto
{
    public required DateOnly Date { get; init; }
    public required long TotalSeconds { get; init; }
    public required long BillableSeconds { get; init; }
    public required decimal BillablePct { get; init; }
    public required int EntryCount { get; init; }
    public required int MembersLogged { get; init; }
    public required long UnassignedSeconds { get; init; }
}

public sealed class ActiveTimerOverviewDto
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

public sealed class IdleMemberOverviewDto
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}

public sealed class OverviewProjectHoursDto
{
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required long TotalSeconds { get; init; }
}

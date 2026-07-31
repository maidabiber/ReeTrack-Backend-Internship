using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Contracts;

/// <summary>Shared Mode + TargetHours body for org settings and member overrides.</summary>
public sealed class HourTargetPayload
{
    public required string Mode { get; init; }
    public required decimal TargetHours { get; init; }
}

public sealed class UserHourTargetResponse
{
    public required Guid UserId { get; init; }
    public required string Mode { get; init; }
    public required decimal TargetHours { get; init; }
}

public sealed class EffectiveHourTargetResponse
{
    public required string Mode { get; init; }
    public required decimal TargetHours { get; init; }
    public required bool IsOverride { get; init; }
    public required bool IsWorkdayToday { get; init; }
    public required IReadOnlyList<string> HolidayDates { get; init; }
}

using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Models;

public sealed class HourTargetSettingsDto
{
    public required HourTargetMode Mode { get; init; }
    public required decimal TargetHours { get; init; }
}

public sealed class UserHourTargetDto
{
    public required Guid UserId { get; init; }
    public required HourTargetMode Mode { get; init; }
    public required decimal TargetHours { get; init; }
}

public sealed class EffectiveHourTargetDto
{
    public required HourTargetMode Mode { get; init; }
    public required decimal TargetHours { get; init; }
    public required bool IsOverride { get; init; }
    public required bool IsWorkdayToday { get; init; }
    public required IReadOnlyList<string> HolidayDates { get; init; }
}

using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Constants;

public static class HourTargetDefaults
{
    public static readonly Guid SettingsId = Guid.Parse("cccccccc-dddd-eeee-ffff-aaaaaaaaaaaa");
    public const HourTargetMode Mode = HourTargetMode.Daily;
    public const decimal TargetHours = 8m;
}

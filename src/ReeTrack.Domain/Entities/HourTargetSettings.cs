using ReeTrack.Domain.Common;
using ReeTrack.Domain.Constants;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;

namespace ReeTrack.Domain.Entities;

public class HourTargetSettings : BaseEntity
{
    public HourTargetMode Mode { get; private set; } = HourTargetDefaults.Mode;
    public decimal TargetHours { get; private set; } = HourTargetDefaults.TargetHours;

    // Required by EF Core materialization.
    private HourTargetSettings()
    {
    }

    public static HourTargetSettings CreateDefault(Guid id, DateTime utcNow)
    {
        return new HourTargetSettings
        {
            Id = id,
            Mode = HourTargetDefaults.Mode,
            TargetHours = HourTargetDefaults.TargetHours,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };
    }

    public void Update(HourTargetMode mode, decimal targetHours)
    {
        HourTargetRules.EnsureValid(mode, targetHours);
        Mode = mode;
        TargetHours = targetHours;
    }
}

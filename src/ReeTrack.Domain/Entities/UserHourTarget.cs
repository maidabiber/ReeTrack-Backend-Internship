using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;

namespace ReeTrack.Domain.Entities;

public class UserHourTarget : BaseEntity
{
    public Guid UserId { get; private set; }
    public HourTargetMode Mode { get; private set; }
    public decimal TargetHours { get; private set; }

    public User User { get; private set; } = null!;

    // Required by EF Core materialization.
    private UserHourTarget()
    {
    }

    public static UserHourTarget Create(Guid userId, HourTargetMode mode, decimal targetHours, DateTime utcNow)
    {
        HourTargetRules.EnsureValid(mode, targetHours);

        return new UserHourTarget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Mode = mode,
            TargetHours = targetHours,
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

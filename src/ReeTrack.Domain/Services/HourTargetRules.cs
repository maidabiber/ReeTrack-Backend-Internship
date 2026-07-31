using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Exceptions;

namespace ReeTrack.Domain.Services;

public static class HourTargetRules
{
    public static void EnsureValid(HourTargetMode mode, decimal targetHours)
    {
        if (!Enum.IsDefined(mode))
            throw new DomainException("Hour target mode must be Daily or Weekly.");

        if (targetHours <= 0m)
            throw new DomainException("Target hours must be greater than zero.");

        var max = mode == HourTargetMode.Daily ? 24m : 168m;
        if (targetHours > max)
        {
            throw new DomainException(
                mode == HourTargetMode.Daily
                    ? "Daily target hours cannot exceed 24."
                    : "Weekly target hours cannot exceed 168.");
        }
    }
}

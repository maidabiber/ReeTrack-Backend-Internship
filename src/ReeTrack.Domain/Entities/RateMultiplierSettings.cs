using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class RateMultiplierSettings : BaseEntity
{
    public decimal WeekendPremium { get; set; } = 0.5m;
    public decimal HolidayPremium { get; set; } = 1.0m;
    public decimal OvertimePremium { get; set; } = 0.5m;
    public decimal WeeklyOvertimeThresholdHours { get; set; } = 40m;
}

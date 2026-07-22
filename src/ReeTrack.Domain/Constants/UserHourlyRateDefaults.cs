using ReeTrack.Domain.ValueObjects;

namespace ReeTrack.Domain.Constants;

public static class UserHourlyRateDefaults
{
    /// <summary>
    /// Baseline hourly wage seeded for every user (German Mindestlohn-style floor).
    /// </summary>
    public static Money MinimumWage { get; } = Money.Eur(12.82m);
}

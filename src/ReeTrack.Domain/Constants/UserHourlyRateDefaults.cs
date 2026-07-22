using ReeTrack.Domain.ValueObjects;

namespace ReeTrack.Domain.Constants;

public static class UserHourlyRateDefaults
{
    /// <summary>
    /// Baseline hourly wage seeded for every user (German Mindestlohn-style floor).
    /// Returns a fresh <see cref="Money"/> on every access: the value is owned by
    /// <c>UserHourlyRate</c>, so sharing one instance across users trips EF Core's
    /// change tracker ("Money.UserHourlyRateId is part of a key…") as soon as more
    /// than one new user is created in a single DbContext — e.g. a batch invite.
    /// </summary>
    public static Money MinimumWage => Money.Eur(12.82m);
}

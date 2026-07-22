using ReeTrack.Domain.Common;
using ReeTrack.Domain.Exceptions;
using ReeTrack.Domain.ValueObjects;

namespace ReeTrack.Domain.Entities;

public class UserHourlyRate : BaseEntity, IAuditable
{
    public Guid UserId { get; private set; }
    public Money Rate { get; private set; } = null!;
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }

    public User User { get; private set; } = null!;

    // Required by EF Core materialization.
    private UserHourlyRate()
    {
    }

    internal static UserHourlyRate CreateOpen(Guid userId, Money rate, DateOnly validFrom)
    {
        if (rate.Amount <= 0)
            throw new DomainException("Hourly rate must be greater than zero.");

        return new UserHourlyRate
        {
            UserId = userId,
            Rate = rate,
            ValidFrom = validFrom,
            ValidTo = null
        };
    }

    internal void UpdateRate(Money rate)
    {
        if (rate.Amount <= 0)
            throw new DomainException("Hourly rate must be greater than zero.");

        Rate = rate;
    }

    internal void SetValidity(DateOnly validFrom, DateOnly? validTo)
    {
        if (validTo is DateOnly to && to < validFrom)
            throw new DomainException("Hourly rate valid-to must be on or after valid-from.");

        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    internal void CloseEndingDayBefore(DateOnly nextValidFrom)
    {
        var validTo = nextValidFrom.AddDays(-1);
        if (validTo < ValidFrom)
            throw new DomainException("Hourly rate valid-from must be after the current period start.");

        ValidTo = validTo;
    }

    public bool Covers(DateOnly date) =>
        date >= ValidFrom && (ValidTo is null || date <= ValidTo);
}

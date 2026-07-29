using ReeTrack.Domain.Common;
using ReeTrack.Domain.Constants;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Exceptions;
using ReeTrack.Domain.ValueObjects;

namespace ReeTrack.Domain.Entities;

public class User : BaseEntity, IAuditable
{
    public string Email { get; set; } = string.Empty;
    public string? GoogleSub { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserRole> AssignedRoles { get; set; } = [];
    public ICollection<Invitation> SentInvitations { get; set; } = [];
    public ICollection<Invitation> AcceptedInvitations { get; set; } = [];
    public ICollection<TimeEntry> TimeEntries { get; set; } = [];
    public ICollection<TimeEntryTemplate> TimeEntryTemplates { get; set; } = [];
    public ICollection<ProjectTask> AssignedTasks { get; set; } = [];
    public ICollection<UserHourlyRate> HourlyRates { get; set; } = [];
    public ICollection<NotificationPreference> NotificationPreferences { get; set; } = [];

    public UserHourlyRate AssignInitialHourlyRate(DateOnly validFrom)
    {
        if (HourlyRates.Count > 0)
            throw new DomainException("User already has an hourly rate assigned.");

        if (Id == Guid.Empty)
            Id = Guid.NewGuid();

        var rate = UserHourlyRate.CreateOpen(Id, UserHourlyRateDefaults.MinimumWage, validFrom);
        HourlyRates.Add(rate);
        return rate;
    }

    public UserHourlyRate ChangeHourlyRate(Money newRate, DateOnly validFrom)
    {
        if (newRate.Amount <= 0)
            throw new DomainException("Hourly rate must be greater than zero.");

        var current = HourlyRates.SingleOrDefault(r => r.ValidTo is null)
            ?? throw new DomainException("User has no current open hourly rate.");

        if (validFrom <= current.ValidFrom)
            throw new DomainException("New hourly rate must start after the current period start.");

        current.CloseEndingDayBefore(validFrom);

        var next = UserHourlyRate.CreateOpen(Id, newRate, validFrom);
        HourlyRates.Add(next);
        return next;
    }

    public UserHourlyRate CorrectHourlyRate(
        Guid rateId,
        Money newRate,
        DateOnly validFrom,
        DateOnly? validTo)
    {
        if (newRate.Amount <= 0)
            throw new DomainException("Hourly rate must be greater than zero.");

        if (validTo is DateOnly to && to < validFrom)
            throw new DomainException("Hourly rate valid-to must be on or after valid-from.");

        var ordered = HourlyRates.OrderBy(r => r.ValidFrom).ToList();
        var index = ordered.FindIndex(r => r.Id == rateId);
        if (index < 0)
            throw new DomainException("Hourly rate was not found.");

        var target = ordered[index];
        var previous = index > 0 ? ordered[index - 1] : null;
        var next = index < ordered.Count - 1 ? ordered[index + 1] : null;

        if (validTo is null && next is not null)
            throw new DomainException("Only the last hourly rate period may be open-ended.");

        if (previous is not null)
        {
            var previousValidTo = validFrom.AddDays(-1);
            if (previousValidTo < previous.ValidFrom)
                throw new DomainException("Corrected period overlaps or eliminates the previous period.");

            previous.SetValidity(previous.ValidFrom, previousValidTo);
        }

        if (next is not null)
        {
            if (validTo is null)
                throw new DomainException("Only the last hourly rate period may be open-ended.");

            var nextValidFrom = validTo.Value.AddDays(1);
            if (next.ValidTo is DateOnly nextTo && nextValidFrom > nextTo)
                throw new DomainException("Corrected period overlaps or eliminates the next period.");

            next.SetValidity(nextValidFrom, next.ValidTo);
        }

        target.UpdateRate(newRate);
        target.SetValidity(validFrom, validTo);
        return target;
    }

    public UserHourlyRate GetHourlyRateOn(DateOnly date)
    {
        var rate = HourlyRates.SingleOrDefault(r => r.Covers(date));
        if (rate is null)
            throw new DomainException($"No hourly rate covers {date:yyyy-MM-dd}.");

        return rate;
    }
}

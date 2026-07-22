using ReeTrack.Domain.Constants;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Exceptions;
using ReeTrack.Domain.ValueObjects;
using Xunit;

namespace ReeTrack.UnitTests.UserHourlyRates;

public class MoneyTests
{
    [Fact]
    public void Of_RoundsToTwoDecimals_AndNormalizesCurrency()
    {
        var money = Money.Of(12.825m, " eur ");

        Assert.Equal(12.83m, money.Amount);
        Assert.Equal("EUR", money.CurrencyCode);
    }

    [Fact]
    public void Of_NegativeAmount_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => Money.Of(-1m, "EUR"));
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Of_InvalidCurrency_Throws()
    {
        Assert.Throws<DomainException>(() => Money.Of(1m, "EU"));
        Assert.Throws<DomainException>(() => Money.Of(1m, ""));
    }

    [Fact]
    public void Eur_CreatesEuroMoney()
    {
        var money = Money.Eur(12.82m);
        Assert.Equal(12.82m, money.Amount);
        Assert.Equal("EUR", money.CurrencyCode);
    }

    [Fact]
    public void Equality_IsByAmountAndCurrency()
    {
        Assert.Equal(Money.Eur(10m), Money.Of(10m, "EUR"));
        Assert.NotEqual(Money.Eur(10m), Money.Of(10m, "USD"));
    }
}

public class UserHourlyRateAggregateTests
{
    [Fact]
    public void AssignInitialHourlyRate_SeedsMinimumWageOpenEnded()
    {
        var user = CreateUser(new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        var from = new DateOnly(2026, 1, 10);

        var rate = user.AssignInitialHourlyRate(from);

        Assert.Equal(UserHourlyRateDefaults.MinimumWage, rate.Rate);
        Assert.Equal(from, rate.ValidFrom);
        Assert.Null(rate.ValidTo);
        Assert.Single(user.HourlyRates);
    }

    [Fact]
    public void AssignInitialHourlyRate_WhenAlreadyAssigned_Throws()
    {
        var user = CreateUser(DateTime.UtcNow);
        user.AssignInitialHourlyRate(DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Throws<DomainException>(() =>
            user.AssignInitialHourlyRate(DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public void ChangeHourlyRate_ClosesPreviousDayBeforeNewStart_WithNoGap()
    {
        var user = CreateUser(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        user.AssignInitialHourlyRate(new DateOnly(2026, 1, 1));

        var next = user.ChangeHourlyRate(Money.Eur(20m), new DateOnly(2026, 3, 15));

        var previous = Assert.Single(user.HourlyRates, r => r.ValidTo is not null);
        Assert.Equal(new DateOnly(2026, 3, 14), previous.ValidTo);
        Assert.Equal(new DateOnly(2026, 3, 15), next.ValidFrom);
        Assert.Null(next.ValidTo);
        Assert.Equal(20m, next.Rate.Amount);

        Assert.Equal(previous.Rate, user.GetHourlyRateOn(new DateOnly(2026, 3, 14)).Rate);
        Assert.Equal(next.Rate, user.GetHourlyRateOn(new DateOnly(2026, 3, 15)).Rate);
    }

    [Fact]
    public void ChangeHourlyRate_Sequence_LeavesNoUncoveredDay()
    {
        var user = CreateUser(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        user.AssignInitialHourlyRate(new DateOnly(2026, 1, 1));
        user.ChangeHourlyRate(Money.Eur(15m), new DateOnly(2026, 2, 1));
        user.ChangeHourlyRate(Money.Eur(18m), new DateOnly(2026, 4, 1));

        for (var date = new DateOnly(2026, 1, 1); date <= new DateOnly(2026, 4, 10); date = date.AddDays(1))
        {
            var rate = user.GetHourlyRateOn(date);
            Assert.True(rate.Covers(date));
        }

        Assert.Equal(3, user.HourlyRates.Count);
        Assert.Single(user.HourlyRates, r => r.ValidTo is null);
    }

    [Fact]
    public void ChangeHourlyRate_ValidFromOnOrBeforeCurrentStart_Throws()
    {
        var user = CreateUser(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        user.AssignInitialHourlyRate(new DateOnly(2026, 1, 1));

        Assert.Throws<DomainException>(() =>
            user.ChangeHourlyRate(Money.Eur(20m), new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void ChangeHourlyRate_ZeroOrNegative_Throws()
    {
        var user = CreateUser(DateTime.UtcNow);
        user.AssignInitialHourlyRate(DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Throws<DomainException>(() =>
            user.ChangeHourlyRate(Money.Eur(0m), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)));
    }

    [Fact]
    public void GetHourlyRateOn_UncoveredDate_Throws()
    {
        var user = CreateUser(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        user.AssignInitialHourlyRate(new DateOnly(2026, 2, 1));

        Assert.Throws<DomainException>(() => user.GetHourlyRateOn(new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void CorrectHourlyRate_UpdatesAmountAndAdjustsNeighbors()
    {
        var user = CreateUser(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var first = user.AssignInitialHourlyRate(new DateOnly(2026, 1, 1));
        first.Id = Guid.NewGuid();
        var second = user.ChangeHourlyRate(Money.Eur(20m), new DateOnly(2026, 3, 1));
        second.Id = Guid.NewGuid();
        var third = user.ChangeHourlyRate(Money.Eur(25m), new DateOnly(2026, 6, 1));
        third.Id = Guid.NewGuid();

        user.CorrectHourlyRate(
            second.Id,
            Money.Eur(22m),
            new DateOnly(2026, 2, 15),
            new DateOnly(2026, 5, 31));

        Assert.Equal(22m, second.Rate.Amount);
        Assert.Equal(new DateOnly(2026, 2, 15), second.ValidFrom);
        Assert.Equal(new DateOnly(2026, 5, 31), second.ValidTo);
        Assert.Equal(new DateOnly(2026, 2, 14), first.ValidTo);
        Assert.Equal(new DateOnly(2026, 6, 1), third.ValidFrom);
        Assert.Null(third.ValidTo);

        for (var date = new DateOnly(2026, 1, 1); date <= new DateOnly(2026, 6, 10); date = date.AddDays(1))
            Assert.True(user.GetHourlyRateOn(date).Covers(date));
    }

    [Fact]
    public void CorrectHourlyRate_UnknownId_Throws()
    {
        var user = CreateUser(DateTime.UtcNow);
        var rate = user.AssignInitialHourlyRate(DateOnly.FromDateTime(DateTime.UtcNow));
        rate.Id = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            user.CorrectHourlyRate(Guid.NewGuid(), Money.Eur(15m), new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void CorrectHourlyRate_OpenEndedBeforeNext_Throws()
    {
        var user = CreateUser(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var first = user.AssignInitialHourlyRate(new DateOnly(2026, 1, 1));
        first.Id = Guid.NewGuid();
        var second = user.ChangeHourlyRate(Money.Eur(20m), new DateOnly(2026, 3, 1));
        second.Id = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            user.CorrectHourlyRate(first.Id, Money.Eur(15m), new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void CorrectHourlyRate_NeighborCollapse_Throws()
    {
        var user = CreateUser(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var first = user.AssignInitialHourlyRate(new DateOnly(2026, 1, 1));
        first.Id = Guid.NewGuid();
        var second = user.ChangeHourlyRate(Money.Eur(20m), new DateOnly(2026, 3, 1));
        second.Id = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            user.CorrectHourlyRate(
                second.Id,
                Money.Eur(20m),
                new DateOnly(2025, 12, 1),
                null));
    }

    private static User CreateUser(DateTime createdAtUtc) =>
        new()
        {
            Email = "user@reetrack.test",
            Status = UserStatus.Active,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
}

using System.Text.RegularExpressions;
using ReeTrack.Domain.Exceptions;

namespace ReeTrack.Domain.ValueObjects;

public sealed partial class Money : IEquatable<Money>
{
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = null!;

    // Required by EF Core owned-type materialization.
    private Money()
    {
    }

    private Money(decimal amount, string currencyCode)
    {
        Amount = amount;
        CurrencyCode = currencyCode;
    }

    public static Money Of(decimal amount, string currencyCode)
    {
        if (amount < 0)
            throw new DomainException("Money amount cannot be negative.");

        // Structural rule: Must be exactly 3 letters
        if (string.IsNullOrWhiteSpace(currencyCode) || !MyRegex().IsMatch(currencyCode.Trim()))
            throw new DomainException("Currency code must be a 3-letter ISO 4217 code.");

        var normalizedCurrency = currencyCode.Trim().ToUpperInvariant();
        var roundedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        return new Money(roundedAmount, normalizedCurrency);
    }

    public static Money Eur(decimal amount) => Of(amount, "EUR");

    public bool Equals(Money? other) =>
        other is not null && Amount == other.Amount && CurrencyCode == other.CurrencyCode;

    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Amount, CurrencyCode);

    public static bool operator ==(Money? left, Money? right) => Equals(left, right);

    public static bool operator !=(Money? left, Money? right) => !Equals(left, right);

    public override string ToString() => $"{Amount:0.00} {CurrencyCode}";
    [GeneratedRegex("^[A-Za-z]{3}$")]
    private static partial Regex MyRegex();
}

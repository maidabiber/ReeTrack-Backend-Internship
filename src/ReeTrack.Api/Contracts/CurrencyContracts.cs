namespace ReeTrack.Api.Contracts;

public sealed class CurrenciesResponse
{
    public required IReadOnlyList<CurrencyResponse> Items { get; init; }
}

public sealed class CurrencyResponse
{
    public required string Code { get; init; }
    public required string Name { get; init; }
}

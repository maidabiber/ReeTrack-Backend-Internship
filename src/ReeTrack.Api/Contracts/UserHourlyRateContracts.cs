namespace ReeTrack.Api.Contracts;

public sealed class ChangeUserHourlyRateRequest
{
    public required decimal HourlyRate { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public string? CurrencyCode { get; init; }
}

public sealed class CorrectUserHourlyRateRequest
{
    public required decimal HourlyRate { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    public string? CurrencyCode { get; init; }
}

public sealed class UserHourlyRateResponse
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required decimal HourlyRate { get; init; }
    public required string CurrencyCode { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
}

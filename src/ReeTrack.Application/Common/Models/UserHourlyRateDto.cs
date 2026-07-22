namespace ReeTrack.Application.Common.Models;

public sealed class UserHourlyRateDto
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required decimal HourlyRate { get; init; }
    public required string CurrencyCode { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
}

public sealed class ChangeUserHourlyRateInput
{
    public required decimal HourlyRate { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public string? CurrencyCode { get; init; }
}

public sealed class CorrectUserHourlyRateInput
{
    public required decimal HourlyRate { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    public string? CurrencyCode { get; init; }
}

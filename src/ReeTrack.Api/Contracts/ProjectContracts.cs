namespace ReeTrack.Api.Contracts;

public sealed class CreateProjectRequest
{
    public string? Name { get; set; }
    public Guid? ClientId { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? FixedFeeAmount { get; set; }
    public decimal? TimeEstimateHours { get; set; }
    public string? Color { get; set; }
}

public sealed class UpdateProjectRequest
{
    public string? Name { get; set; }
    public Guid? ClientId { get; set; }
    public string? Status { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? FixedFeeAmount { get; set; }
    public decimal? TimeEstimateHours { get; set; }
    public string? Color { get; set; }
}

public sealed class ProjectResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid ClientId { get; init; }
    public required string ClientName { get; init; }
    public required string Status { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal? HourlyRate { get; init; }
    public required decimal? FixedFeeAmount { get; init; }
    public required decimal? TimeEstimateHours { get; init; }
    public required string? Color { get; init; }
    public required int TaskCount { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

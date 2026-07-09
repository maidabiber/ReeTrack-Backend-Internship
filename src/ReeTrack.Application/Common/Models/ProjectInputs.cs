namespace ReeTrack.Application.Common.Models;

public sealed class CreateProjectInput
{
    public string? Name { get; init; }
    public Guid? ClientId { get; init; }
    public string? BillingType { get; init; }
    public string? CurrencyCode { get; init; }
    public decimal? HourlyRate { get; init; }
    public decimal? FixedFeeAmount { get; init; }
    public decimal? BudgetAmount { get; init; }
    public decimal? TimeEstimateHours { get; init; }
    public string? Color { get; init; }
}

// Patch semantics: Name/ClientId/Status apply only when present. The billing
// block (currency, rate, fee, budget, estimate, color) is applied wholesale
// whenever BillingType is present — null clears — because the edit form always
// sends the full block, while lightweight patches (archive) send status alone.
public sealed class UpdateProjectInput
{
    public string? Name { get; init; }
    public Guid? ClientId { get; init; }
    public string? Status { get; init; }
    public string? BillingType { get; init; }
    public string? CurrencyCode { get; init; }
    public decimal? HourlyRate { get; init; }
    public decimal? FixedFeeAmount { get; init; }
    public decimal? BudgetAmount { get; init; }
    public decimal? TimeEstimateHours { get; init; }
    public string? Color { get; init; }
}

public sealed class CreateTaskInput
{
    public string? Name { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public decimal? TimeEstimateHours { get; init; }
}

// Status alone toggles open/done. When Name is present the request is a full
// content update: AssignedToUserId and TimeEstimateHours are applied as sent
// (null clears), matching the edit form which always sends every field.
public sealed class UpdateTaskInput
{
    public string? Name { get; init; }
    public string? Status { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public decimal? TimeEstimateHours { get; init; }
}

namespace ReeTrack.Api.Contracts;

public sealed class GenerateInvoiceRequest
{
    /// <summary>Filter payload (same shape as report queries); must include exactly one client.</summary>
    public ReportQueryRequest? Query { get; init; }
}

public sealed class InvoiceResponse
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required Guid ClientId { get; init; }
    public required string ClientName { get; init; }
    public required string CurrencyCode { get; init; }
    public required DateOnly PeriodFrom { get; init; }
    public required DateOnly PeriodTo { get; init; }
    public required decimal Subtotal { get; init; }
    public required string Status { get; init; }
    public required Guid GeneratedByUserId { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public Guid? NewerInvoiceId { get; init; }
    public string? NewerInvoiceNumber { get; init; }
    public required IReadOnlyList<InvoiceLineItemResponse> LineItems { get; init; }
}

public sealed class InvoiceLineItemResponse
{
    public required Guid Id { get; init; }
    public required Guid? ProjectId { get; init; }
    public required string Description { get; init; }
    public required string BillingModel { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal Amount { get; init; }
    public required int SortOrder { get; init; }
}

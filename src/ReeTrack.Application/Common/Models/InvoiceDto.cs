using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Models;

public sealed class InvoiceDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required Guid ClientId { get; init; }
    public required string ClientName { get; init; }
    public required string CurrencyCode { get; init; }
    public required DateOnly PeriodFrom { get; init; }
    public required DateOnly PeriodTo { get; init; }
    public required decimal Subtotal { get; init; }
    public required InvoiceStatus Status { get; init; }
    public required Guid GeneratedByUserId { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public Guid? NewerInvoiceId { get; init; }
    public string? NewerInvoiceNumber { get; init; }
    public required IReadOnlyList<InvoiceLineItemDto> LineItems { get; init; }
}

public sealed class InvoiceLineItemDto
{
    public required Guid Id { get; init; }
    public required Guid? ProjectId { get; init; }
    public required string Description { get; init; }
    public required InvoiceLineBillingModel BillingModel { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal Amount { get; init; }
    public required int SortOrder { get; init; }
}

public sealed class GenerateInvoiceInput
{
    /// <summary>Filter payload; must include exactly one client.</summary>
    public required ReportQuery Query { get; init; }
}

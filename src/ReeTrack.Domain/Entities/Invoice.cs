using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class Invoice : BaseEntity, ISoftDeletable, IAuditable
{
    public string Number { get; set; } = string.Empty;

    public Guid ClientId { get; set; }

    /// <summary>Client display name at generation time (survives later renames).</summary>
    public string ClientName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "EUR";

    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }

    public decimal Subtotal { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public Guid GeneratedByUserId { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Client Client { get; set; } = null!;
    public User GeneratedByUser { get; set; } = null!;
    public ICollection<InvoiceLineItem> LineItems { get; set; } = [];
}

using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class InvoiceLineItem : BaseEntity
{
    public Guid InvoiceId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Description { get; set; } = string.Empty;

    public InvoiceLineBillingModel BillingModel { get; set; }

    /// <summary>Billable hours for hourly lines; 1 for fixed-fee lines.</summary>
    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }

    public int SortOrder { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public Project? Project { get; set; }
}

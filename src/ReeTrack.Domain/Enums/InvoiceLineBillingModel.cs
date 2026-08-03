namespace ReeTrack.Domain.Enums;

/// <summary>How a persisted invoice line was priced (snapshot at generation time).</summary>
public enum InvoiceLineBillingModel : short
{
    Hourly = 0,
    FixedFee = 1
}

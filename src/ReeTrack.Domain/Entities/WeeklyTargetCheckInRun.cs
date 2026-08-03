using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

/// <summary>
/// Idempotency ledger for Friday weekly target check-in runs (one row per local week Monday).
/// </summary>
public class WeeklyTargetCheckInRun : BaseEntity
{
    public DateOnly WeekStartDate { get; set; }
    public DateTime RanAtUtc { get; set; }
}

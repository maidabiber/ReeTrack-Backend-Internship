namespace ReeTrack.Infrastructure.Auditing;

/// <summary>
/// Append-only audit record, written exclusively by <see cref="AuditSaveChangesInterceptor"/>
/// in the same transaction as the change it describes. Deliberately not a BaseEntity
/// (no UpdatedAtUtc — rows are never updated) and not exposed on IApplicationDbContext.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }

    /// <summary>Null when the change was made without an authenticated user (setup, seeding, jobs).</summary>
    public Guid? ActorUserId { get; set; }

    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

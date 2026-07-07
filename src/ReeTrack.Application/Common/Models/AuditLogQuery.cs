namespace ReeTrack.Application.Common.Models;

public sealed class AuditLogQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public Guid? ActorUserId { get; init; }

    /// <summary>Created, Updated, Deleted or Restored (case-insensitive).</summary>
    public string? Action { get; init; }

    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}

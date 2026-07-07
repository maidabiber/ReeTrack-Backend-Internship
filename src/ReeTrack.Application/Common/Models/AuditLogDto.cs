namespace ReeTrack.Application.Common.Models;

public sealed class AuditLogDto
{
    public required Guid Id { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Action { get; init; }
    public Guid? ActorUserId { get; init; }

    /// <summary>Resolved for display; null when the change was made by the system.</summary>
    public string? ActorEmail { get; init; }

    /// <summary>Raw JSON — the frontend parses these.</summary>
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }

    public required DateTime OccurredAtUtc { get; init; }
}

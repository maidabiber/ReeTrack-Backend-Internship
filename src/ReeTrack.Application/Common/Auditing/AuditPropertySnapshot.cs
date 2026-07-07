namespace ReeTrack.Application.Common.Auditing;

/// <summary>
/// Provider-agnostic snapshot of a single entity property at save time,
/// so the diff builder can be unit tested without EF Core.
/// </summary>
public sealed record AuditPropertySnapshot(
    string Name,
    object? OriginalValue,
    object? CurrentValue,
    bool IsModified);

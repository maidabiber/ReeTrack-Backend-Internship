using ReeTrack.Domain.Entities;

namespace ReeTrack.Application.Common.Auditing;

/// <summary>
/// Registry of properties whose values must never appear in audit logs.
/// The property is still recorded (so the audit shows it changed) but masked.
/// </summary>
public static class AuditRedaction
{
    public const string Mask = "[REDACTED]";

    private static readonly HashSet<(string EntityType, string Property)> Sensitive =
    [
        (nameof(Invitation), nameof(Invitation.TokenHash)),
        (nameof(User), nameof(User.GoogleSub)),
    ];

    public static bool IsSensitive(string entityType, string propertyName) =>
        Sensitive.Contains((entityType, propertyName));
}

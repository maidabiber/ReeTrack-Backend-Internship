namespace ReeTrack.Application.Notifications;

/// <summary>
/// Content delivered through one or more notification channels.
/// </summary>
public sealed class NotificationPayload
{
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}

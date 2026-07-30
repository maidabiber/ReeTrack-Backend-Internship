namespace ReeTrack.Domain.Entities;

/// <summary>
/// Persisted in-app notification delivered to a specific user.
/// </summary>
public class InAppNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}

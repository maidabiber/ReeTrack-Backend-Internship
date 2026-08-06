using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class JiraWebhookSettings : BaseEntity
{
    public short SingletonKey { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime? LastReceivedAtUtc { get; set; }
}

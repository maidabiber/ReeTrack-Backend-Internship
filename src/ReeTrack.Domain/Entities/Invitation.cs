using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class Invitation : BaseEntity, IAuditable
{
    public string Email { get; set; } = string.Empty;
    public short RoleId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public InvitationStatus Status { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public Guid InvitedByUserId { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public Guid? AcceptedByUserId { get; set; }

    public Role Role { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
    public User? AcceptedByUser { get; set; }
}

namespace ReeTrack.Domain.Entities;

public class UserRole
{
    public Guid UserId { get; set; }
    public short RoleId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public Guid? AssignedByUserId { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public User? AssignedByUser { get; set; }
}

namespace ReeTrack.Domain.Entities;

public class Role
{
    public short Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<Invitation> Invitations { get; set; } = [];
}

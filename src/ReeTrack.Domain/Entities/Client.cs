using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class Client : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public ICollection<Project> Projects { get; set; } = [];
}

using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class Client : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Project> Projects { get; set; } = [];
}

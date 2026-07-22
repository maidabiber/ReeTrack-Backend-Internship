using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class Holiday : BaseEntity
{
    public DateOnly Date { get; set; }
}

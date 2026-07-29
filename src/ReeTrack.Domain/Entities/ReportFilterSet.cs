using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class ReportFilterSet : BaseEntity, IAuditable
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string QueryJson { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;

    public User User { get; set; } = null!;
}

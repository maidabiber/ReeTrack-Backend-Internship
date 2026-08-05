using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class CustomReportDefinition : BaseEntity, IAuditable, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SpecJson { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }

    /// <summary>Shared is visible to every admin; Private is visible only to its creator.</summary>
    public CustomReportVisibility Visibility { get; set; } = CustomReportVisibility.Shared;

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public User CreatedByUser { get; set; } = null!;
}

using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class ReportShareLink : BaseEntity, IAuditable
{
    public string Token { get; set; } = string.Empty;
    public ReportShareReportType ReportType { get; set; }
    public Guid? ReportId { get; set; }
    public string? QueryJson { get; set; }
    public string? SpecJson { get; set; }
    public ReportShareAccessLevel AccessLevel { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsActive { get; set; } = true;

    public User CreatedByUser { get; set; } = null!;
    public ICollection<ReportShareRecipient> Recipients { get; set; } = [];
}

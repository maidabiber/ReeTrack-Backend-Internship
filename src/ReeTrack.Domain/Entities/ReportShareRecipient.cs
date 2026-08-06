using ReeTrack.Domain.Common;

namespace ReeTrack.Domain.Entities;

public class ReportShareRecipient : BaseEntity
{
    public Guid ShareLinkId { get; set; }
    public Guid UserId { get; set; }

    public ReportShareLink ShareLink { get; set; } = null!;
    public User User { get; set; } = null!;
}

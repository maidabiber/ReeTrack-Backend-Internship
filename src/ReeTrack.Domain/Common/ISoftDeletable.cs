namespace ReeTrack.Domain.Common;

public interface ISoftDeletable
{
    DateTime? DeletedAtUtc { get; set; }
    Guid? DeletedByUserId { get; set; }
}

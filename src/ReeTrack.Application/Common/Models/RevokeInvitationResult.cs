namespace ReeTrack.Application.Common.Models;

public sealed class RevokeInvitationResult
{
    public required InvitationDto Invitation { get; init; }

    /// <summary>
    /// Set when revoking also deleted the placeholder user row (the invitee
    /// never signed in), so callers can drop them from member lists.
    /// </summary>
    public Guid? RemovedUserId { get; init; }
}

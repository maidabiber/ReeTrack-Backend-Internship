namespace ReeTrack.Application.Common.Models;

public sealed class CreateInvitationResult
{
    public required MemberDto Member { get; init; }
    public required InvitationDto Invitation { get; init; }
}

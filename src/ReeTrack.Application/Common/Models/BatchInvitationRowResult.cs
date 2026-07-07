namespace ReeTrack.Application.Common.Models;

public enum BatchInvitationRowStatus
{
    Invited = 0,
    AlreadyActive = 1,
    Invalid = 2,
    EmailFailed = 3,
    Duplicate = 4
}

/// <summary>
/// Per-email outcome of a batch invite. One bad address never fails the whole
/// batch; EmailFailed means the invitation was saved but the email could not
/// be delivered (resend is available).
/// </summary>
public sealed class BatchInvitationRowResult
{
    public required string Email { get; init; }
    public required BatchInvitationRowStatus Status { get; init; }
    public string? Message { get; init; }
    public MemberDto? Member { get; init; }
}

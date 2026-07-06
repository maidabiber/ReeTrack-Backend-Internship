namespace ReeTrack.Application.Common.Models;

public sealed class InvitationPreviewDto
{
    public required string InvitedEmail { get; init; }
    public required string InviterName { get; init; }
    public required string Role { get; init; }
    public required string AppName { get; init; }
}

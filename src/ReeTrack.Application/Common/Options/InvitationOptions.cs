namespace ReeTrack.Application.Common.Options;

public class InvitationOptions
{
    public const string SectionName = "Invitation";

    public int ExpiryDays { get; set; } = 7;
}

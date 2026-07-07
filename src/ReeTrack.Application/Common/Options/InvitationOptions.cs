namespace ReeTrack.Application.Common.Options;

public class InvitationOptions
{
    public const string SectionName = "Invitation";

    public int ExpiryDays { get; set; } = 7;

    /// <summary>
    /// Email domains allowed to be invited, mirroring the domain(s) your Google
    /// SSO accepts. Configure via <c>Invitation__AllowedDomains__0</c>,
    /// <c>Invitation__AllowedDomains__1</c>, ... An empty list allows any domain,
    /// which keeps the check a no-op unless it is explicitly configured.
    /// </summary>
    public string[] AllowedDomains { get; set; } = [];
}

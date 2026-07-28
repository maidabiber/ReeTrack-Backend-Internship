namespace ReeTrack.Application.Common.Options;

public class JiraOptions
{
    public const string SectionName = "Jira";

    public string SiteUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SiteUrl)
        && !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(ApiToken);
}

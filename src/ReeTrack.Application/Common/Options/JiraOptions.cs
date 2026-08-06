namespace ReeTrack.Application.Common.Options;

public class JiraOptions
{
    public const string SectionName = "Jira";

    public string SiteUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret used to verify Jira Cloud webhook signatures (X-Hub-Signature).
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SiteUrl)
        && !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(ApiToken);

    public bool IsWebhookConfigured => !string.IsNullOrWhiteSpace(WebhookSecret);
}

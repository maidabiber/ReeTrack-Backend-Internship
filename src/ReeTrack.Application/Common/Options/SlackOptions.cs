namespace ReeTrack.Application.Common.Options;

public class SlackOptions
{
    public const string SectionName = "Slack";

    /// <summary>Bot User OAuth Token (xoxb-...).</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Workspace invite URL shown on Profile when the user is not a Slack member.</summary>
    public string InviteUrl { get; set; } = string.Empty;

    /// <summary>Minimum delay between outbound Slack notification sends.</summary>
    public int SendDelayMilliseconds { get; set; } = 1000;

    public string BaseUrl { get; set; } = "https://slack.com/api/";
}

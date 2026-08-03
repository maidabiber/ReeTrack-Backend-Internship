namespace ReeTrack.Application.Integrations.Slack;

/// <summary>
/// Thin Slack Web API client for user lookup and direct messages.
/// </summary>
public interface ISlackApiClient
{
    /// <summary>
    /// Looks up a Slack user id by email. Returns null when the user is not in the workspace
    /// or Slack is not configured.
    /// </summary>
    Task<string?> LookupUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a DM with the Slack user and posts <paramref name="text"/>.
    /// Applies the configured send delay between notification sends.
    /// </summary>
    Task SendDirectMessageAsync(string slackUserId, string text, CancellationToken cancellationToken = default);
}

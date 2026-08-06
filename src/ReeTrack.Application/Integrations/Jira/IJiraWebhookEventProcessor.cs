namespace ReeTrack.Application.Integrations.Jira;

public interface IJiraWebhookEventProcessor
{
    /// <summary>
    /// Applies a signed Jira webhook payload. Skips unsupported events, subtasks,
    /// inactive settings, and projects that are not integrated.
    /// </summary>
    Task ProcessAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
}

namespace ReeTrack.Application.Integrations.Jira;

public interface IJiraWebhookSubscriptionService
{
    Task<bool> ValidateSignatureAsync(
        ReadOnlyMemory<byte> payload,
        string? signature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns false only when settings exist and are explicitly inactive.
    /// Missing settings row is treated as active (secret comes from env).
    /// </summary>
    Task<bool> IsReceiveActiveAsync(CancellationToken cancellationToken = default);

    Task MarkReceivedAsync(CancellationToken cancellationToken = default);
}

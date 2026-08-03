namespace ReeTrack.Application.Integrations.Slack;

public sealed class SlackStatusDto
{
    public required bool IsConfigured { get; init; }
    public required bool IsMember { get; init; }
    public string? InviteUrl { get; init; }
}

public interface ISlackIntegrationService
{
    Task<SlackStatusDto> GetStatusForCurrentUserAsync(CancellationToken cancellationToken = default);
}

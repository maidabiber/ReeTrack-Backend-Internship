namespace ReeTrack.Application.Integrations.Calendar.Models;

public class OAuthTokenSet
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
    public string? ProviderAccountId { get; init; }
}

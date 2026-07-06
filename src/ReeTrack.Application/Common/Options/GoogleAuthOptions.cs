namespace ReeTrack.Application.Common.Options;

public class GoogleAuthOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The redirect_uri registered in Google Cloud Console and sent during authorize + token exchange.
    /// Must match the same origin as the SPA (e.g. http://localhost:5173/api/auth/google/callback).
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Optional. When set, only this email may become the first admin on initial setup.
    /// </summary>
    public string? AdminEmail { get; set; }
}

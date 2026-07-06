namespace ReeTrack.Application.Common.Interfaces;

public interface IGoogleOAuthService
{
    string GenerateState();

    /// <summary>
    /// Validates and normalizes a post-error return path. Rejects open redirects.
    /// </summary>
    string ValidateReturnUrl(string? returnUrl);

    string BuildAuthorizationUrl(string state);
}

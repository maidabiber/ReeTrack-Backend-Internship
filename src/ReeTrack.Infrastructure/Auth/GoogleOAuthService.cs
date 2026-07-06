using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;

namespace ReeTrack.Infrastructure.Auth;

public class GoogleOAuthService : IGoogleOAuthService
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

    private static readonly HashSet<string> AllowedReturnPaths =
        new(StringComparer.OrdinalIgnoreCase) { "/", "/signin", "/onboarding" };

    private readonly GoogleAuthOptions _options;

    public GoogleOAuthService(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    public string ValidateReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//"))
            return "/";

        var path = returnUrl.Split('?', '#')[0];
        return AllowedReturnPaths.Contains(path) ? path : "/";
    }

    public string BuildAuthorizationUrl(string state)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("Google ClientId is not configured.");

        if (string.IsNullOrWhiteSpace(_options.RedirectUri))
            throw new InvalidOperationException("Google RedirectUri is not configured.");

        var query = string.Join("&", new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "online",
            ["prompt"] = "select_account"
        }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return $"{AuthorizationEndpoint}?{query}";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

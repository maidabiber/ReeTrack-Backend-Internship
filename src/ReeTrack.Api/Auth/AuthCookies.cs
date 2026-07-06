using Microsoft.AspNetCore.Http;

namespace ReeTrack.Api.Auth;

internal static class AuthCookies
{
    internal const string SessionCookieName = "rt.session";
    internal const string OAuthStateCookieName = "rt.oauth.state";
    internal const string OAuthReturnUrlCookieName = "rt.oauth.returnUrl";

    private static readonly TimeSpan OAuthCookieLifetime = TimeSpan.FromMinutes(10);

    internal static void SetOAuthCookies(
        HttpResponse response,
        string state,
        string returnUrl,
        bool secure)
    {
        var options = CreateOAuthCookieOptions(secure, OAuthCookieLifetime);

        response.Cookies.Append(OAuthStateCookieName, state, options);
        response.Cookies.Append(OAuthReturnUrlCookieName, returnUrl, options);
    }

    internal static void ClearOAuthCookies(HttpResponse response, bool secure)
    {
        var options = CreateOAuthCookieOptions(secure, TimeSpan.Zero);

        response.Cookies.Delete(OAuthStateCookieName, options);
        response.Cookies.Delete(OAuthReturnUrlCookieName, options);
    }

    internal static void SetSessionCookie(
        HttpResponse response,
        string accessToken,
        DateTime expiresAtUtc,
        bool secure)
    {
        response.Cookies.Append(SessionCookieName, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAtUtc,
            Path = "/"
        });
    }

    internal static void ClearSessionCookie(HttpResponse response, bool secure)
    {
        response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }

    private static CookieOptions CreateOAuthCookieOptions(bool secure, TimeSpan lifetime) =>
        new()
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.Add(lifetime),
            Path = "/"
        };
}

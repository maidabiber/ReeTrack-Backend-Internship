using Microsoft.AspNetCore.Http;

namespace ReeTrack.Api.Auth;

internal static class IntegrationOAuthCookies
{
    internal const string StateCookieName = "rt.calendar.oauth.state";
    internal const string ReturnUrlCookieName = "rt.calendar.oauth.returnUrl";
    internal const string UserIdCookieName = "rt.calendar.oauth.userId";

    private static readonly TimeSpan OAuthCookieLifetime = TimeSpan.FromMinutes(10);

    internal static void SetOAuthCookies(
        HttpResponse response,
        string state,
        string returnUrl,
        Guid userId,
        bool secure)
    {
        var options = CreateOAuthCookieOptions(secure, OAuthCookieLifetime);

        response.Cookies.Append(StateCookieName, state, options);
        response.Cookies.Append(ReturnUrlCookieName, returnUrl, options);
        response.Cookies.Append(UserIdCookieName, userId.ToString(), options);
    }

    internal static void ClearOAuthCookies(HttpResponse response, bool secure)
    {
        var options = CreateOAuthCookieOptions(secure, TimeSpan.Zero);

        response.Cookies.Delete(StateCookieName, options);
        response.Cookies.Delete(ReturnUrlCookieName, options);
        response.Cookies.Delete(UserIdCookieName, options);
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

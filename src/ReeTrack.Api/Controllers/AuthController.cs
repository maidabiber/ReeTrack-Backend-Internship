using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using ReeTrack.Api.Auth;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IGoogleOAuthService _googleOAuthService;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        IAuthService authService,
        IGoogleOAuthService googleOAuthService,
        IWebHostEnvironment environment)
    {
        _authService = authService;
        _googleOAuthService = googleOAuthService;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpGet("google")]
    public IActionResult StartGoogleSignIn([FromQuery] string? returnUrl)
    {
        try
        {
            var validatedReturnUrl = _googleOAuthService.ValidateReturnUrl(returnUrl);
            var state = _googleOAuthService.GenerateState();

            AuthCookies.SetOAuthCookies(Response, state, validatedReturnUrl, UseSecureCookies());

            var authorizationUrl = _googleOAuthService.BuildAuthorizationUrl(state);
            return Redirect(authorizationUrl);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var returnUrl = Request.Cookies[AuthCookies.OAuthReturnUrlCookieName] ?? "/";
        returnUrl = _googleOAuthService.ValidateReturnUrl(returnUrl);

        var storedState = Request.Cookies[AuthCookies.OAuthStateCookieName];
        AuthCookies.ClearOAuthCookies(Response, UseSecureCookies());

        if (!string.IsNullOrWhiteSpace(error))
            return RedirectWithAuthError(returnUrl, "Google sign-in was cancelled or failed.");

        if (string.IsNullOrWhiteSpace(code))
            return RedirectWithAuthError(returnUrl, "Authorization code is missing.");

        if (string.IsNullOrWhiteSpace(state) || storedState is null || !string.Equals(state, storedState, StringComparison.Ordinal))
            return RedirectWithAuthError(returnUrl, "Invalid OAuth state. Please try signing in again.");

        try
        {
            var result = await _authService.SignInWithGoogleAsync(code, cancellationToken);

            AuthCookies.SetSessionCookie(Response, result.AccessToken, result.ExpiresAtUtc, UseSecureCookies());

            return Redirect("/");
        }
        catch (AuthException ex)
        {
            return RedirectWithAuthError(returnUrl, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedUser>> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);
            return Ok(user);
        }
        catch (AuthException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        AuthCookies.ClearSessionCookie(Response, UseSecureCookies());
        return Ok(new { message = "Signed out successfully." });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(claim, out userId);
    }

    private RedirectResult RedirectWithAuthError(string returnUrl, string message)
    {
        var redirectUrl = QueryHelpers.AddQueryString(returnUrl, "authError", message);
        return Redirect(redirectUrl);
    }

    private bool UseSecureCookies() => !_environment.IsDevelopment();
}

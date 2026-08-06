using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using ReeTrack.Api.Auth;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IGoogleOAuthService _googleOAuthService;
    private readonly ICurrentUserService _currentUser;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IGoogleOAuthService googleOAuthService,
        ICurrentUserService currentUser,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _googleOAuthService = googleOAuthService;
        _currentUser = currentUser;
        _environment = environment;
        _logger = logger;
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
            _logger.LogError(ex, "Failed to start Google sign-in");
            throw new AppException("Could not start Google sign-in. Please try again.", 500);
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
            _logger.LogError(ex, "Failed to complete Google sign-in callback");
            return RedirectWithAuthError(returnUrl, "Google sign-in is temporarily unavailable. Please try again.");
        }
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedUser>> GetCurrentUser(CancellationToken cancellationToken)
    {
        try
        {
            var user = await _authService.GetCurrentUserAsync(_currentUser.UserId, cancellationToken);
            return Ok(user);
        }
        catch (AuthException ex)
        {
            throw new AppException(ex.Message, ex.StatusCode, ex.Code);
        }
    }

    [HttpPost("onboarding-complete")]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken cancellationToken)
    {
        try
        {
            await _authService.MarkOnboardingCompleteAsync(_currentUser.UserId, cancellationToken);
            return NoContent();
        }
        catch (AuthException ex)
        {
            throw new AppException(ex.Message, ex.StatusCode, ex.Code);
        }
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        AuthCookies.ClearSessionCookie(Response, UseSecureCookies());
        return Ok(new { message = "Signed out successfully." });
    }

    private RedirectResult RedirectWithAuthError(string returnUrl, string message)
    {
        var redirectUrl = QueryHelpers.AddQueryString(returnUrl, "authError", message);
        return Redirect(redirectUrl);
    }

    private bool UseSecureCookies() => !_environment.IsDevelopment();
}

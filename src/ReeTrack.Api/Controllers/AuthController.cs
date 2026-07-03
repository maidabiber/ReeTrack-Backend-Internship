using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<ActionResult<GoogleSignInResponse>> SignInWithGoogle(
        [FromBody] GoogleSignInRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            return BadRequest(new { message = "Google ID token is required." });

        try
        {
            var result = await _authService.SignInWithGoogleAsync(request.IdToken, cancellationToken);

            return Ok(new GoogleSignInResponse
            {
                AccessToken = result.AccessToken,
                ExpiresAtUtc = result.ExpiresAtUtc,
                User = new AuthenticatedUserResponse
                {
                    Id = result.User.Id,
                    Email = result.User.Email,
                    DisplayName = result.User.DisplayName,
                    AvatarUrl = result.User.AvatarUrl,
                    Roles = result.User.Roles
                }
            });
        }
        catch (AuthException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }
}

public sealed class GoogleSignInRequest
{
    public string IdToken { get; set; } = string.Empty;
}

public sealed class GoogleSignInResponse
{
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required AuthenticatedUserResponse User { get; init; }
}

public sealed class AuthenticatedUserResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}

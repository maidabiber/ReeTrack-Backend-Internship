using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using ReeTrack.Api.Auth;
using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/integrations/calendar")]
public class CalendarIntegrationsController : ControllerBase
{
    private readonly ICalendarIntegrationService _calendarIntegrationService;
    private readonly ICalendarSyncService _calendarSyncService;
    private readonly IWebHostEnvironment _environment;

    public CalendarIntegrationsController(
        ICalendarIntegrationService calendarIntegrationService,
        ICalendarSyncService calendarSyncService,
        IWebHostEnvironment environment)
    {
        _calendarIntegrationService = calendarIntegrationService;
        _calendarSyncService = calendarSyncService;
        _environment = environment;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ListConnections(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var connections = await _calendarIntegrationService.ListConnectionsAsync(userId, cancellationToken);
        return Ok(connections);
    }

    [Authorize]
    [HttpGet("google/connect")]
    public IActionResult StartGoogleConnect([FromQuery] string? returnUrl)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var validatedReturnUrl = _calendarIntegrationService.ValidateReturnUrl(returnUrl);
            var state = _calendarIntegrationService.GenerateState();

            IntegrationOAuthCookies.SetOAuthCookies(Response, state, validatedReturnUrl, userId, UseSecureCookies());

            var authorizationUrl = _calendarIntegrationService.BuildConnectUrl(CalendarProviderType.Google, state);
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
        var returnUrl = Request.Cookies[IntegrationOAuthCookies.ReturnUrlCookieName] ?? "/";
        returnUrl = _calendarIntegrationService.ValidateReturnUrl(returnUrl);

        var storedState = Request.Cookies[IntegrationOAuthCookies.StateCookieName];
        var storedUserId = Request.Cookies[IntegrationOAuthCookies.UserIdCookieName];
        IntegrationOAuthCookies.ClearOAuthCookies(Response, UseSecureCookies());

        if (!Guid.TryParse(storedUserId, out var userId))
            return RedirectWithIntegrationError(returnUrl, "Calendar connection session expired. Please try connecting again.");

        if (!string.IsNullOrWhiteSpace(error))
            return RedirectWithIntegrationError(returnUrl, "Google Calendar connection was cancelled or failed.");

        if (string.IsNullOrWhiteSpace(code))
            return RedirectWithIntegrationError(returnUrl, "Authorization code is missing.");

        if (string.IsNullOrWhiteSpace(state) || storedState is null || !string.Equals(state, storedState, StringComparison.Ordinal))
            return RedirectWithIntegrationError(returnUrl, "Invalid OAuth state. Please try connecting again.");

        try
        {
            await _calendarIntegrationService.CompleteConnectAsync(
                userId,
                CalendarProviderType.Google,
                code,
                cancellationToken);

            return Redirect(returnUrl);
        }
        catch (CalendarIntegrationException ex)
        {
            return RedirectWithIntegrationError(returnUrl, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Disconnect(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            await _calendarIntegrationService.DisconnectAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (CalendarIntegrationException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("{id:guid}/sync")]
    public async Task<IActionResult> Sync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var connections = await _calendarIntegrationService.ListConnectionsAsync(userId, cancellationToken);
        if (connections.All(c => c.Id != id))
            return NotFound(new { message = "Calendar connection not found." });

        try
        {
            await _calendarSyncService.SyncConnectionAsync(id, cancellationToken);
            return Ok(new { message = "Calendar sync completed." });
        }
        catch (CalendarIntegrationException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(claim, out userId);
    }

    private RedirectResult RedirectWithIntegrationError(string returnUrl, string message)
    {
        var redirectUrl = QueryHelpers.AddQueryString(returnUrl, "integrationError", message);
        return Redirect(redirectUrl);
    }

    private bool UseSecureCookies() => !_environment.IsDevelopment();
}

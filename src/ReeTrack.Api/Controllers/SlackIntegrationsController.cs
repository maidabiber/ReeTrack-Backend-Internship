using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Integrations.Slack;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/integrations/slack")]
[Authorize]
public class SlackIntegrationsController : ControllerBase
{
    private readonly ISlackIntegrationService _slack;

    public SlackIntegrationsController(ISlackIntegrationService slack)
    {
        _slack = slack;
    }

    [HttpGet("status")]
    public async Task<ActionResult<SlackStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _slack.GetStatusForCurrentUserAsync(cancellationToken);
        return Ok(new SlackStatusResponse
        {
            IsConfigured = status.IsConfigured,
            IsMember = status.IsMember,
            InviteUrl = status.InviteUrl
        });
    }
}

public sealed class SlackStatusResponse
{
    public required bool IsConfigured { get; init; }
    public required bool IsMember { get; init; }
    public string? InviteUrl { get; init; }
}

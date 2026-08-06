using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Integrations.Jira;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/webhooks/jira/events")]
[AllowAnonymous]
public class JiraWebhookEventsController : ControllerBase
{
    private const int MaxPayloadBytes = 1024 * 1024;

    private readonly IJiraWebhookSubscriptionService _webhooks;
    private readonly IJiraWebhookEventProcessor _processor;
    private readonly ILogger<JiraWebhookEventsController> _logger;

    public JiraWebhookEventsController(
        IJiraWebhookSubscriptionService webhooks,
        IJiraWebhookEventProcessor processor,
        ILogger<JiraWebhookEventsController> logger)
    {
        _webhooks = webhooks;
        _processor = processor;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(MaxPayloadBytes)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var payload = buffer.ToArray();
        var signature = Request.Headers["X-Hub-Signature"].FirstOrDefault();

        if (!await _webhooks.ValidateSignatureAsync(payload, signature, cancellationToken))
            return Unauthorized();

        await _webhooks.MarkReceivedAsync(cancellationToken);

        try
        {
            await _processor.ProcessAsync(payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process a signed Jira webhook ({PayloadLength} bytes).", payload.Length);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return NoContent();
    }
}

using Microsoft.Extensions.Logging;
using ReeTrack.Application.Integrations.Jira;

namespace ReeTrack.Infrastructure.Integrations.Jira;

public sealed class JiraWebhookEventProcessor : IJiraWebhookEventProcessor
{
    private readonly IJiraWebhookSubscriptionService _webhooks;
    private readonly IJiraIntegrationService _jira;
    private readonly ILogger<JiraWebhookEventProcessor> _logger;

    public JiraWebhookEventProcessor(
        IJiraWebhookSubscriptionService webhooks,
        IJiraIntegrationService jira,
        ILogger<JiraWebhookEventProcessor> logger)
    {
        _webhooks = webhooks;
        _jira = jira;
        _logger = logger;
    }

    public async Task ProcessAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (!await _webhooks.IsReceiveActiveAsync(cancellationToken))
        {
            _logger.LogInformation("Ignoring Jira webhook because receive is inactive.");
            return;
        }

        if (!JiraWebhookPayloadParser.TryParse(payload.Span, out var parsed) || parsed is null)
        {
            _logger.LogInformation("Ignoring unsupported or incomplete Jira webhook payload.");
            return;
        }

        if (parsed.IsSubtask)
        {
            _logger.LogInformation(
                "Ignoring Jira subtask webhook for {IssueKey}.",
                parsed.Issue.Key);
            return;
        }

        var applied = await _jira.ApplyRemoteIssueAsync(
            parsed.JiraProjectId,
            parsed.JiraProjectKey,
            parsed.Issue,
            cancellationToken);

        if (applied)
        {
            _logger.LogInformation(
                "Applied Jira {WebhookEvent} for {IssueKey} in project {ProjectKey}.",
                parsed.WebhookEvent,
                parsed.Issue.Key,
                parsed.JiraProjectKey);
        }
        else
        {
            _logger.LogInformation(
                "Ignored Jira {WebhookEvent} for {IssueKey}; project {ProjectKey} is not integrated.",
                parsed.WebhookEvent,
                parsed.Issue.Key,
                parsed.JiraProjectKey);
        }
    }
}

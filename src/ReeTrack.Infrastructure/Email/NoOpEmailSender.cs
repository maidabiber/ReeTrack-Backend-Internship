using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Infrastructure.Email;

/// <summary>
/// Fallback used when no SMTP host is configured: logs the invite URL
/// so the flow stays usable in local development without email credentials.
/// </summary>
public class NoOpEmailSender : IEmailSender
{
    private readonly ILogger<NoOpEmailSender> _logger;

    public NoOpEmailSender(ILogger<NoOpEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendInviteEmailAsync(
        string toEmail,
        string inviteUrl,
        string inviterName,
        string roleName,
        string appName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Email delivery is not configured (Email__SmtpHost is empty). " +
            "Invite for {ToEmail} as {RoleName}: {InviteUrl}",
            toEmail, roleName, inviteUrl);

        return Task.CompletedTask;
    }
}

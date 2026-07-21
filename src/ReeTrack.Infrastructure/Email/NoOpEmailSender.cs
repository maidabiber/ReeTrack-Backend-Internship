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

    public Task SendTimeEntryMentionEmailAsync(
        string toEmail,
        string assigneeName,
        string submitterName,
        string? description,
        string reviewUrl,
        string appName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Email delivery is not configured (Email__SmtpHost is empty). " +
            "Time entry mention for {ToEmail} from {SubmitterName}: {ReviewUrl}",
            toEmail, submitterName, reviewUrl);

        return Task.CompletedTask;
    }

    public Task SendTimesheetDecisionEmailAsync(
        string toEmail,
        string recipientName,
        string reviewerName,
        string weekLabel,
        bool approved,
        string? comment,
        string timesheetUrl,
        string appName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Email delivery is not configured (Email__SmtpHost is empty). " +
            "Timesheet {Decision} for {ToEmail} ({WeekLabel}) by {ReviewerName}: {TimesheetUrl}",
            approved ? "approval" : "rejection", toEmail, weekLabel, reviewerName, timesheetUrl);

        return Task.CompletedTask;
    }
}

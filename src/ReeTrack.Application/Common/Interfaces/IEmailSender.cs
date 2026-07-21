namespace ReeTrack.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendInviteEmailAsync(
        string toEmail,
        string inviteUrl,
        string inviterName,
        string roleName,
        string appName,
        CancellationToken cancellationToken = default);

    Task SendTimeEntryMentionEmailAsync(
        string toEmail,
        string assigneeName,
        string submitterName,
        string? description,
        string reviewUrl,
        string appName,
        CancellationToken cancellationToken = default);

    Task SendTimesheetDecisionEmailAsync(
        string toEmail,
        string recipientName,
        string reviewerName,
        string weekLabel,
        bool approved,
        string? comment,
        string timesheetUrl,
        string appName,
        CancellationToken cancellationToken = default);
}

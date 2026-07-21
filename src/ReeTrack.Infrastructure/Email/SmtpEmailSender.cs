using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;

namespace ReeTrack.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public Task SendInviteEmailAsync(
        string toEmail,
        string inviteUrl,
        string inviterName,
        string roleName,
        string appName,
        CancellationToken cancellationToken = default)
    {
        var subject = $"{inviterName} invited you to {appName}";
        var textBody =
            $"{inviterName} invited you to join {appName} as a {roleName}.\n\n" +
            $"Accept your invite: {inviteUrl}\n\n" +
            "Sign in with the Google account that matches this email address.";

        // Names come from user-controlled data (e.g. Google display names), so escape them.
        var htmlBody =
            $"""
            <p><strong>{WebUtility.HtmlEncode(inviterName)}</strong> invited you to join <strong>{WebUtility.HtmlEncode(appName)}</strong> as a <strong>{WebUtility.HtmlEncode(roleName)}</strong>.</p>
            <p><a href="{WebUtility.HtmlEncode(inviteUrl)}">Accept your invite</a></p>
            <p>Sign in with the Google account that matches this email address.</p>
            """;

        return SendAsync(toEmail, subject, textBody, htmlBody, cancellationToken);
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
        var subject = $"{submitterName} shared a time entry with you on {appName}";
        var descriptionLine = string.IsNullOrWhiteSpace(description)
            ? "No description provided."
            : description.Trim();

        var textBody =
            $"{submitterName} logged time on your behalf in {appName}.\n\n" +
            $"Description: {descriptionLine}\n\n" +
            $"Review and approve: {reviewUrl}";

        var htmlBody =
            $"""
            <p><strong>{WebUtility.HtmlEncode(submitterName)}</strong> logged time on your behalf in <strong>{WebUtility.HtmlEncode(appName)}</strong>.</p>
            <p><strong>Description:</strong> {WebUtility.HtmlEncode(descriptionLine)}</p>
            <p><a href="{WebUtility.HtmlEncode(reviewUrl)}">Review and approve</a></p>
            """;

        return SendAsync(toEmail, subject, textBody, htmlBody, cancellationToken);
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
        var decision = approved ? "approved" : "rejected";
        var subject = $"Your timesheet for {weekLabel} was {decision} on {appName}";

        var commentLine = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        var callToAction = approved
            ? "View your timesheet"
            : "Fix your entries and resubmit";

        var textBody =
            $"Hi {recipientName},\n\n" +
            $"{reviewerName} {decision} your timesheet for {weekLabel} in {appName}.\n\n" +
            (commentLine is null ? "" : $"Comment: {commentLine}\n\n") +
            $"{callToAction}: {timesheetUrl}";

        var htmlBody =
            $"""
            <p>Hi {WebUtility.HtmlEncode(recipientName)},</p>
            <p><strong>{WebUtility.HtmlEncode(reviewerName)}</strong> {decision} your timesheet for <strong>{WebUtility.HtmlEncode(weekLabel)}</strong> in <strong>{WebUtility.HtmlEncode(appName)}</strong>.</p>
            """ +
            (commentLine is null
                ? ""
                : $"""<p><strong>Comment:</strong> {WebUtility.HtmlEncode(commentLine)}</p>""") +
            $"""<p><a href="{WebUtility.HtmlEncode(timesheetUrl)}">{callToAction}</a></p>""";

        return SendAsync(toEmail, subject, textBody, htmlBody, cancellationToken);
    }

    private async Task SendAsync(
        string toEmail,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
            throw new InvalidOperationException("SMTP host is not configured.");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = textBody, HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
            await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}

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

    public async Task SendInviteEmailAsync(
        string toEmail,
        string inviteUrl,
        string inviterName,
        string roleName,
        string appName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
            throw new InvalidOperationException("SMTP host is not configured.");

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

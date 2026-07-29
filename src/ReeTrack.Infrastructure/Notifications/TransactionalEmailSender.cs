using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Notifications;

namespace ReeTrack.Infrastructure.Notifications;

/// <summary>
/// Shared SMTP (or console-log) email delivery used by transactional sends and channel providers.
/// </summary>
public sealed class TransactionalEmailSender : ITransactionalEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<TransactionalEmailSender> _logger;

    public TransactionalEmailSender(
        IOptions<EmailOptions> options,
        ILogger<TransactionalEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(body);

        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogWarning(
                "Email delivery is not configured (Email__SmtpHost is empty). " +
                "Email for {ToEmail}: {Subject} — {Body}",
                toEmail,
                subject,
                body);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = body }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _options.SmtpHost,
            _options.SmtpPort,
            SecureSocketOptions.StartTlsWhenAvailable,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
            await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogInformation("Sent email to {ToEmail}: {Subject}", toEmail, subject);
    }
}

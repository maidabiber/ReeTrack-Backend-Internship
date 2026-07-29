using System.Text.RegularExpressions;
using ReeTrack.Application.Notifications;

namespace ReeTrack.IntegrationTests.Support;

public sealed class FakeTransactionalEmailSender : ITransactionalEmailSender
{
    private static readonly Regex InviteUrlRegex = new(
        @"Accept your invite:\s*(\S+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string? LastToEmail { get; private set; }
    public string? LastInviteUrl { get; private set; }
    public string? LastSubject { get; private set; }
    public string? LastBody { get; private set; }

    public Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        LastToEmail = toEmail;
        LastSubject = subject;
        LastBody = body;

        var match = InviteUrlRegex.Match(body);
        LastInviteUrl = match.Success ? match.Groups[1].Value : null;

        return Task.CompletedTask;
    }
}

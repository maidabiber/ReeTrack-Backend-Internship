using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.IntegrationTests.Support;

public sealed class FakeEmailSender : IEmailSender
{
    public string? LastToEmail { get; private set; }
    public string? LastInviteUrl { get; private set; }

    public Task SendInviteEmailAsync(
        string toEmail,
        string inviteUrl,
        string inviterName,
        string roleName,
        string appName,
        CancellationToken cancellationToken = default)
    {
        LastToEmail = toEmail;
        LastInviteUrl = inviteUrl;
        return Task.CompletedTask;
    }
}

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
}

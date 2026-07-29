namespace ReeTrack.Application.Notifications;

/// <summary>
/// Sends email to an explicit address (transactional / forced delivery, no preferences).
/// </summary>
public interface ITransactionalEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}

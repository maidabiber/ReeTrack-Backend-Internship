using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Notifications;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Notifications;

/// <summary>
/// Delivers notifications over email by resolving the user's address and using
/// <see cref="ITransactionalEmailSender"/>.
/// </summary>
public sealed class EmailChannelProvider : IChannelProvider
{
    private readonly ITransactionalEmailSender _emailSender;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<EmailChannelProvider> _logger;

    public EmailChannelProvider(
        ITransactionalEmailSender emailSender,
        IApplicationDbContext db,
        ILogger<EmailChannelProvider> logger)
    {
        _emailSender = emailSender;
        _db = db;
        _logger = logger;
    }

    public DeliveryChannel ChannelType => DeliveryChannel.Email;

    public async Task SendAsync(
        Guid userId,
        NotificationPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var toEmail = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning(
                "Skipping email notification for user {UserId}: recipient email is missing.",
                userId);
            return;
        }

        await _emailSender.SendAsync(toEmail, payload.Subject, payload.Body, cancellationToken);
    }
}

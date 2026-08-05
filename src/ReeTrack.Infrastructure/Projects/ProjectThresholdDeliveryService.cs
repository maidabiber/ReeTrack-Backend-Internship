using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Notifications;
using ReeTrack.Application.Notifications.Events;

namespace ReeTrack.Infrastructure.Projects;

public sealed class ProjectThresholdDeliveryService : IProjectThresholdDeliveryService
{
    private readonly IApplicationDbContext _db;
    private readonly IProjectThresholdRecipientResolver _recipientResolver;
    private readonly IDomainEventPublisher _eventPublisher;
    private readonly ILogger<ProjectThresholdDeliveryService> _logger;

    public ProjectThresholdDeliveryService(
        IApplicationDbContext db,
        IProjectThresholdRecipientResolver recipientResolver,
        IDomainEventPublisher eventPublisher,
        ILogger<ProjectThresholdDeliveryService> logger)
    {
        _db = db;
        _recipientResolver = recipientResolver;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<int> DeliverPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var pending = await _db.PendingProjectAlerts
            .Where(a => a.DeliveredAtUtc == null && a.DeliverAfterUtc <= now)
            .OrderBy(a => a.DeliverAfterUtc)
            .ThenBy(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return 0;

        var recipients = await _recipientResolver.GetRecipientsAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Found {PendingCount} pending project threshold alerts, but no Admin recipients were resolved.",
                pending.Count);
        }

        var notificationsDelivered = 0;

        foreach (var alert in pending)
        {
            foreach (var recipient in recipients)
            {
                await _eventPublisher.PublishAsync(
                    new ProjectThresholdAlertNotification
                    {
                        RecipientUserId = recipient.UserId,
                        RecipientName = recipient.DisplayName,
                        ProjectId = alert.ProjectId,
                        ProjectName = alert.ProjectName,
                        MetricType = alert.MetricType,
                        ThresholdPercentage = alert.ThresholdPercentage,
                        CostPercentage = alert.CostPercentage,
                        CalculatedCost = alert.CalculatedCost,
                        FixedFeeAmount = alert.FixedFeeAmount,
                        CurrencyCode = alert.CurrencyCode,
                        HoursPercentage = alert.HoursPercentage,
                        ActualHours = alert.ActualHours,
                        TimeEstimateHours = alert.TimeEstimateHours
                    },
                    cancellationToken);

                notificationsDelivered++;
            }

            alert.DeliveredAtUtc = now;
            alert.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return notificationsDelivered;
    }
}

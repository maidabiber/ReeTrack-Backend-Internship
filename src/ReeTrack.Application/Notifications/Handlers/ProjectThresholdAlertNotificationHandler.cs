using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Notifications.Events;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications.Handlers;

public sealed class ProjectThresholdAlertNotificationHandler
    : IDomainEventHandler<ProjectThresholdAlertNotification>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<ProjectThresholdAlertNotificationHandler> _logger;
    private readonly string _frontendOrigin;
    private readonly string _appName;

    public ProjectThresholdAlertNotificationHandler(
        INotificationDispatcher dispatcher,
        ILogger<ProjectThresholdAlertNotificationHandler> logger,
        IConfiguration configuration,
        IOptions<AppOptions> appOptions)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
        _appName = appOptions.Value.Name;
    }

    public async Task HandleAsync(
        ProjectThresholdAlertNotification domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectUrl = $"{_frontendOrigin.TrimEnd('/')}/projects/{domainEvent.ProjectId}";
            var thresholdLabel = domainEvent.ThresholdPercentage.ToString("0.##");

            NotificationPayload payload;
            if (domainEvent.MetricType == ProjectThresholdMetricType.TimeEstimate)
            {
                var hoursPercentLabel = domainEvent.HoursPercentage.ToString("0.##");
                var actualLabel = domainEvent.ActualHours.ToString("0.##");
                var estimateLabel = domainEvent.TimeEstimateHours.ToString("0.##");

                payload = new NotificationPayload
                {
                    Subject =
                        $"Project time alert: {domainEvent.ProjectName} reached {thresholdLabel}% of time estimate on {_appName}",
                    Body =
                        $"Hi {domainEvent.RecipientName},\n\n" +
                        $"Project \"{domainEvent.ProjectName}\" has reached {hoursPercentLabel}% of its time estimate " +
                        $"(threshold: {thresholdLabel}%) on {_appName}.\n\n" +
                        $"Actual hours: {actualLabel}h\n" +
                        $"Time estimate: {estimateLabel}h\n\n" +
                        $"View project: {projectUrl}",
                    Metadata = new Dictionary<string, string>
                    {
                        [NotificationMetadataKeys.FrontendUrl] = projectUrl
                    }
                };
            }
            else
            {
                var costPercentLabel = domainEvent.CostPercentage.ToString("0.##");
                var costLabel = $"{domainEvent.CalculatedCost:0.##} {domainEvent.CurrencyCode}";
                var feeLabel = $"{domainEvent.FixedFeeAmount:0.##} {domainEvent.CurrencyCode}";

                payload = new NotificationPayload
                {
                    Subject =
                        $"Project cost alert: {domainEvent.ProjectName} reached {thresholdLabel}% of fixed fee on {_appName}",
                    Body =
                        $"Hi {domainEvent.RecipientName},\n\n" +
                        $"Project \"{domainEvent.ProjectName}\" has reached {costPercentLabel}% of its fixed fee " +
                        $"(threshold: {thresholdLabel}%) on {_appName}.\n\n" +
                        $"Current cost: {costLabel}\n" +
                        $"Fixed fee: {feeLabel}\n\n" +
                        $"View project: {projectUrl}",
                    Metadata = new Dictionary<string, string>
                    {
                        [NotificationMetadataKeys.FrontendUrl] = projectUrl
                    }
                };
            }

            await _dispatcher.DispatchAsync(
                domainEvent.RecipientUserId,
                NotificationType.ProjectThresholdAlert,
                payload,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Project threshold alert for project {ProjectId} could not be sent to user {RecipientUserId}.",
                domainEvent.ProjectId,
                domainEvent.RecipientUserId);
        }
    }
}

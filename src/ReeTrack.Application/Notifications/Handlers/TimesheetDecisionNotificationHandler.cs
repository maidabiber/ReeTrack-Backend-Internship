using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Notifications.Events;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications.Handlers;

public sealed class TimesheetDecisionNotificationHandler : IDomainEventHandler<TimesheetDecisionNotification>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<TimesheetDecisionNotificationHandler> _logger;
    private readonly string _frontendOrigin;
    private readonly string _appName;

    public TimesheetDecisionNotificationHandler(
        INotificationDispatcher dispatcher,
        ILogger<TimesheetDecisionNotificationHandler> logger,
        IConfiguration configuration,
        IOptions<AppOptions> appOptions)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
        _appName = appOptions.Value.Name;
    }

    public async Task HandleAsync(
        TimesheetDecisionNotification domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var decision = domainEvent.Approved ? "approved" : "rejected";
            var weekLabel = $"the week of {domainEvent.WeekStartDate:dd MMM yyyy}";
            var timesheetUrl =
                $"{_frontendOrigin.TrimEnd('/')}/timesheet?week={domainEvent.WeekStartDate:yyyy-MM-dd}";
            var commentLine = string.IsNullOrWhiteSpace(domainEvent.Comment) ? null : domainEvent.Comment.Trim();
            var callToAction = domainEvent.Approved
                ? "View your timesheet"
                : "Fix your entries and resubmit";

            var payload = new NotificationPayload
            {
                Subject = $"Your timesheet for {weekLabel} was {decision} on {_appName}",
                Body =
                    $"Hi {domainEvent.RecipientName},\n\n" +
                    $"{domainEvent.ReviewerName} {decision} your timesheet for {weekLabel} in {_appName}.\n\n" +
                    (commentLine is null ? "" : $"Comment: {commentLine}\n\n") +
                    $"{callToAction}: {timesheetUrl}"
            };

            await _dispatcher.DispatchAsync(
                domainEvent.RecipientUserId,
                NotificationType.TimesheetDecision,
                payload,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Reviewed timesheet {TimesheetId}, but the decision notification to user {RecipientUserId} could not be sent.",
                domainEvent.TimesheetId,
                domainEvent.RecipientUserId);
        }
    }
}

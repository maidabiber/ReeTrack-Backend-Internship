using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Notifications.Events;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications.Handlers;

public sealed class WeeklyTargetCheckInNotificationHandler : IDomainEventHandler<WeeklyTargetCheckInNotification>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<WeeklyTargetCheckInNotificationHandler> _logger;
    private readonly string _frontendOrigin;
    private readonly string _appName;

    public WeeklyTargetCheckInNotificationHandler(
        INotificationDispatcher dispatcher,
        ILogger<WeeklyTargetCheckInNotificationHandler> logger,
        IConfiguration configuration,
        IOptions<AppOptions> appOptions)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
        _appName = appOptions.Value.Name;
    }

    public async Task HandleAsync(
        WeeklyTargetCheckInNotification domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var timesheetUrl =
                $"{_frontendOrigin.TrimEnd('/')}/timesheet?week={domainEvent.TimesheetWeekStartDate:yyyy-MM-dd}";
            var logged = FormatHours(domainEvent.LoggedHours);
            var target = FormatHours(domainEvent.TargetHours);
            var remaining = FormatHours(domainEvent.RemainingHours);

            string subject;
            string lead;
            if (domainEvent.OnTrack)
            {
                subject = $"On track — week target met on {_appName}";
                lead =
                    $"You've hit your target for this week. Logged {logged} of {target}.";
            }
            else
            {
                subject = $"Almost there — {remaining} left toward your week target on {_appName}";
                lead =
                    $"You're nearly there. Logged {logged} of {target}; {remaining} remaining.";
            }

            var weakestLine = domainEvent.WeakestDay is DateOnly weakest
                ? $"\n\n{weakest:dddd} ({weakest:dd MMM}) had the fewest hours tracked so far: {FormatHours(domainEvent.WeakestDayHours ?? 0)}."
                : "";

            var payload = new NotificationPayload
            {
                Subject = subject,
                Body =
                    $"Hi {domainEvent.RecipientName},\n\n" +
                    $"{lead}{weakestLine}\n\n" +
                    $"Review your timesheet: {timesheetUrl}",
                Metadata = new Dictionary<string, string>
                {
                    [NotificationMetadataKeys.FrontendUrl] = timesheetUrl
                }
            };

            await _dispatcher.DispatchAsync(
                domainEvent.RecipientUserId,
                NotificationType.WeeklyTargetCheckIn,
                payload,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Weekly target check-in notification to user {RecipientUserId} could not be sent.",
                domainEvent.RecipientUserId);
        }
    }

    private static string FormatHours(decimal hours)
    {
        var rounded = Math.Round(hours, 2, MidpointRounding.AwayFromZero);
        return $"{rounded:0.##}h";
    }
}

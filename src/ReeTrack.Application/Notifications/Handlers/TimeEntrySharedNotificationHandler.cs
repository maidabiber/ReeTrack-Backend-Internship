using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Notifications.Events;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Notifications.Handlers;

public sealed class TimeEntrySharedNotificationHandler : IDomainEventHandler<TimeEntrySharedNotification>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<TimeEntrySharedNotificationHandler> _logger;
    private readonly string _frontendOrigin;
    private readonly string _appName;

    public TimeEntrySharedNotificationHandler(
        INotificationDispatcher dispatcher,
        ILogger<TimeEntrySharedNotificationHandler> logger,
        IConfiguration configuration,
        IOptions<AppOptions> appOptions)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
        _appName = appOptions.Value.Name;
    }

    public async Task HandleAsync(
        TimeEntrySharedNotification domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reviewUrl = $"{_frontendOrigin.TrimEnd('/')}/approvals";
            var descriptionLine = string.IsNullOrWhiteSpace(domainEvent.Description)
                ? "No description provided."
                : domainEvent.Description.Trim();

            var payload = new NotificationPayload
            {
                Subject = $"{domainEvent.SubmitterName} shared a time entry with you on {_appName}",
                Body =
                    $"{domainEvent.SubmitterName} logged time on your behalf in {_appName}.\n\n" +
                    $"Description: {descriptionLine}\n\n" +
                    $"Review and approve: {reviewUrl}",
                Metadata = new Dictionary<string, string>
                {
                    [NotificationMetadataKeys.FrontendUrl] = reviewUrl
                }
            };

            await _dispatcher.DispatchAsync(
                domainEvent.AssigneeUserId,
                NotificationType.TimeEntryShared,
                payload,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Saved shared time entry {EntryId} for user {AssigneeUserId}, but notification could not be sent.",
                domainEvent.EntryId,
                domainEvent.AssigneeUserId);
        }
    }
}

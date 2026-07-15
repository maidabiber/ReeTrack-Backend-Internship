using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.TimeEntries;

public class SharedTimeEntryEmailNotifier : ISharedTimeEntryEmailNotifier
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SharedTimeEntryEmailNotifier> _logger;
    private readonly string _frontendOrigin;
    private readonly string _appName;

    public SharedTimeEntryEmailNotifier(
        IEmailSender emailSender,
        ILogger<SharedTimeEntryEmailNotifier> logger,
        IConfiguration configuration,
        IOptions<AppOptions> appOptions)
    {
        _emailSender = emailSender;
        _logger = logger;
        _frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
        _appName = appOptions.Value.Name;
    }

    public void QueueShareNotificationEmails(
        IReadOnlyList<TimeEntry> createdEntries,
        IReadOnlyDictionary<Guid, User> assigneeById,
        string submitterName)
    {
        var reviewUrl = $"{_frontendOrigin.TrimEnd('/')}/approvals";

        foreach (var entry in createdEntries)
        {
            var assignee = assigneeById[entry.UserId];
            var assigneeName = assignee.DisplayName?.Trim() ?? assignee.Email;

            _ = SendShareNotificationEmailAsync(
                entry.Id,
                assignee.Email,
                assigneeName,
                submitterName,
                entry.Description,
                reviewUrl);
        }
    }

    private async Task SendShareNotificationEmailAsync(
        Guid entryId,
        string assigneeEmail,
        string assigneeName,
        string submitterName,
        string? description,
        string reviewUrl)
    {
        try
        {
            await _emailSender.SendTimeEntryMentionEmailAsync(
                assigneeEmail,
                assigneeName,
                submitterName,
                description,
                reviewUrl,
                _appName,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Saved shared time entry {EntryId} for {AssigneeEmail}, but mention email could not be sent.",
                entryId,
                assigneeEmail);
        }
    }
}

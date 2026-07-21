using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Timesheets;

public class TimesheetDecisionEmailNotifier : ITimesheetDecisionEmailNotifier
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<TimesheetDecisionEmailNotifier> _logger;
    private readonly string _frontendOrigin;
    private readonly string _appName;

    public TimesheetDecisionEmailNotifier(
        IEmailSender emailSender,
        ILogger<TimesheetDecisionEmailNotifier> logger,
        IConfiguration configuration,
        IOptions<AppOptions> appOptions)
    {
        _emailSender = emailSender;
        _logger = logger;
        _frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
        _appName = appOptions.Value.Name;
    }

    public void QueueDecisionEmail(Timesheet timesheet, User recipient, string reviewerName, bool approved)
    {
        var weekLabel = $"the week of {timesheet.WeekStartDate:dd MMM yyyy}";
        var timesheetUrl =
            $"{_frontendOrigin.TrimEnd('/')}/timesheet?week={timesheet.WeekStartDate:yyyy-MM-dd}";

        _ = SendDecisionEmailAsync(
            timesheet.Id,
            recipient.Email,
            recipient.DisplayName?.Trim() ?? recipient.Email,
            reviewerName,
            weekLabel,
            approved,
            timesheet.ReviewComment,
            timesheetUrl);
    }

    private async Task SendDecisionEmailAsync(
        Guid timesheetId,
        string toEmail,
        string recipientName,
        string reviewerName,
        string weekLabel,
        bool approved,
        string? comment,
        string timesheetUrl)
    {
        try
        {
            await _emailSender.SendTimesheetDecisionEmailAsync(
                toEmail,
                recipientName,
                reviewerName,
                weekLabel,
                approved,
                comment,
                timesheetUrl,
                _appName,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Reviewed timesheet {TimesheetId}, but the decision email to {ToEmail} could not be sent.",
                timesheetId,
                toEmail);
        }
    }
}

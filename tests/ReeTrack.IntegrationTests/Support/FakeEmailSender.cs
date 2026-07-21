using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.IntegrationTests.Support;

public sealed class FakeEmailSender : IEmailSender
{
    public string? LastToEmail { get; private set; }
    public string? LastInviteUrl { get; private set; }
    public string? LastMentionReviewUrl { get; private set; }
    public string? LastMentionToEmail { get; private set; }

    public Task SendInviteEmailAsync(
        string toEmail,
        string inviteUrl,
        string inviterName,
        string roleName,
        string appName,
        CancellationToken cancellationToken = default)
    {
        LastToEmail = toEmail;
        LastInviteUrl = inviteUrl;
        return Task.CompletedTask;
    }

    public Task SendTimeEntryMentionEmailAsync(
        string toEmail,
        string assigneeName,
        string submitterName,
        string? description,
        string reviewUrl,
        string appName,
        CancellationToken cancellationToken = default)
    {
        LastMentionToEmail = toEmail;
        LastMentionReviewUrl = reviewUrl;
        return Task.CompletedTask;
    }

    public sealed record DecisionEmail(
        string ToEmail,
        string RecipientName,
        string ReviewerName,
        string WeekLabel,
        bool Approved,
        string? Comment,
        string TimesheetUrl);

    public List<DecisionEmail> DecisionEmails { get; } = [];

    public Task SendTimesheetDecisionEmailAsync(
        string toEmail,
        string recipientName,
        string reviewerName,
        string weekLabel,
        bool approved,
        string? comment,
        string timesheetUrl,
        string appName,
        CancellationToken cancellationToken = default)
    {
        DecisionEmails.Add(new DecisionEmail(
            toEmail, recipientName, reviewerName, weekLabel, approved, comment, timesheetUrl));
        return Task.CompletedTask;
    }
}

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ReeTrack.Application.Notifications;
using ReeTrack.Domain.Enums;

namespace ReeTrack.IntegrationTests.Support;

/// <summary>
/// Test double for <see cref="IChannelProvider"/> that captures email-channel notifications.
/// </summary>
public sealed class FakeEmailChannelProvider : IChannelProvider
{
    private static readonly Regex ReviewUrlRegex = new(
        @"Review and approve:\s*(\S+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TimesheetUrlRegex = new(
        @"https?://\S*timesheet\?week=\S+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CommentRegex = new(
        @"Comment:\s*(.+?)(?:\n\n|$)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly ConcurrentBag<(Guid UserId, NotificationPayload Payload)> _sent = [];

    public DeliveryChannel ChannelType => DeliveryChannel.Email;

    public IReadOnlyList<(Guid UserId, NotificationPayload Payload)> Sent => _sent.ToList();

    public Guid? LastMentionUserId { get; private set; }
    public string? LastMentionReviewUrl { get; private set; }

    public sealed record DecisionEmail(
        Guid UserId,
        bool Approved,
        string? Comment,
        string TimesheetUrl);

    public List<DecisionEmail> DecisionEmails { get; } = [];

    public Task SendAsync(
        Guid userId,
        NotificationPayload payload,
        CancellationToken cancellationToken = default)
    {
        _sent.Add((userId, payload));

        if (payload.Body.Contains("/approvals", StringComparison.Ordinal))
        {
            LastMentionUserId = userId;
            var match = ReviewUrlRegex.Match(payload.Body);
            LastMentionReviewUrl = match.Success ? match.Groups[1].Value : null;
        }

        if (payload.Subject.Contains("timesheet", StringComparison.OrdinalIgnoreCase))
        {
            var approved = payload.Subject.Contains("approved", StringComparison.OrdinalIgnoreCase);
            var commentMatch = CommentRegex.Match(payload.Body);
            var urlMatch = TimesheetUrlRegex.Match(payload.Body);

            DecisionEmails.Add(new DecisionEmail(
                userId,
                Approved: approved,
                Comment: commentMatch.Success ? commentMatch.Groups[1].Value.Trim() : null,
                TimesheetUrl: urlMatch.Success ? urlMatch.Value : ""));
        }

        return Task.CompletedTask;
    }
}

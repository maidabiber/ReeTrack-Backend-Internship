using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Slack;

namespace ReeTrack.Infrastructure.Integrations.Slack;

public sealed class SlackIntegrationService : ISlackIntegrationService
{
    private readonly ISlackApiClient _slack;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly SlackOptions _options;

    public SlackIntegrationService(
        ISlackApiClient slack,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IOptions<SlackOptions> options)
    {
        _slack = slack;
        _db = db;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<SlackStatusDto> GetStatusForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var isConfigured = !string.IsNullOrWhiteSpace(_options.BotToken);
        var inviteUrl = string.IsNullOrWhiteSpace(_options.InviteUrl)
            ? null
            : _options.InviteUrl.Trim();

        if (!isConfigured)
        {
            return new SlackStatusDto
            {
                IsConfigured = false,
                IsMember = false,
                InviteUrl = inviteUrl
            };
        }

        var email = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == _currentUser.UserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        var isMember = !string.IsNullOrWhiteSpace(email)
            && !string.IsNullOrWhiteSpace(
                await _slack.LookupUserIdByEmailAsync(email, cancellationToken));

        return new SlackStatusDto
        {
            IsConfigured = true,
            IsMember = isMember,
            InviteUrl = inviteUrl
        };
    }
}

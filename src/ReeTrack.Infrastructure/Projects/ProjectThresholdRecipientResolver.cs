using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Projects;

public sealed class ProjectThresholdRecipientResolver : IProjectThresholdRecipientResolver
{
    private readonly IApplicationDbContext _db;

    public ProjectThresholdRecipientResolver(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProjectThresholdRecipient>> GetRecipientsAsync(
        CancellationToken cancellationToken = default)
    {
        // Admins receive threshold alerts today.
        // When a Project Manager role is introduced, include those users here as well.
        var recipients = await _db.Users
            .AsNoTracking()
            .Where(u =>
                u.Status == UserStatus.Active &&
                u.UserRoles.Any(ur => ur.RoleId == RoleIds.Admin))
            .Select(u => new ProjectThresholdRecipient
            {
                UserId = u.Id,
                DisplayName = u.DisplayName ?? u.Email
            })
            .ToListAsync(cancellationToken);

        return recipients;
    }
}

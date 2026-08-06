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

    public async Task<IReadOnlyList<ProjectThresholdRecipient>> GetRecipientsForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var createdByUserId = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => (Guid?)p.CreatedByUserId)
            .FirstOrDefaultAsync(cancellationToken);

        var recipients = await _db.Users
            .AsNoTracking()
            .Where(u =>
                u.Status == UserStatus.Active &&
                (u.UserRoles.Any(ur => ur.RoleId == RoleIds.Admin) ||
                 (createdByUserId != null && u.Id == createdByUserId)))
            .Select(u => new ProjectThresholdRecipient
            {
                UserId = u.Id,
                DisplayName = u.DisplayName ?? u.Email
            })
            .ToListAsync(cancellationToken);

        return recipients;
    }
}

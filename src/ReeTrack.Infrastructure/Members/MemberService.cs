using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Members;

public class MemberService : IMemberService
{
    private readonly IApplicationDbContext _db;

    public MemberService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Email)
            .ToListAsync(cancellationToken);

        var pendingInvitations = await _db.Invitations
            .AsNoTracking()
            .Where(i => i.Status == InvitationStatus.Pending)
            .ToDictionaryAsync(i => i.Email, i => i.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return users
            .Select(user => MapMember(user, pendingInvitations))
            .ToList();
    }

    internal static MemberDto MapMember(User user, IReadOnlyDictionary<string, Guid>? pendingInvitations = null)
    {
        var role = user.UserRoles.FirstOrDefault()?.Role
            ?? throw new InvalidOperationException($"User {user.Id} has no assigned role.");

        return new MemberDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Role = role.Name,
            RoleId = role.Id,
            Status = user.Status,
            EmailVerified = user.EmailVerified,
            LastLoginAtUtc = user.LastLoginAtUtc,
            PendingInvitationId = pendingInvitations is not null &&
                                  pendingInvitations.TryGetValue(user.Email, out var invitationId)
                ? invitationId
                : null
        };
    }
}

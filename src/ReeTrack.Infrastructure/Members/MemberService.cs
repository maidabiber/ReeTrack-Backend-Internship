using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Members;

public class MemberService : IMemberService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MemberService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<MemberDto>> ListAsync(
        MemberListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.Users.AsNoTracking();

        var q = query.Q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(q))
        {
            filtered = filtered.Where(u =>
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(q)) ||
                u.Email.ToLower().Contains(q));
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        var users = await filtered
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.HourTargets)
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var emails = users.Select(u => u.Email).ToList();
        var pendingInvitations = await _db.Invitations
            .AsNoTracking()
            .Where(i => i.Status == InvitationStatus.Pending && emails.Contains(i.Email))
            .ToDictionaryAsync(i => i.Email, i => i.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return new PagedResult<MemberDto>
        {
            Items = users.Select(user => MapMember(user, pendingInvitations)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<MemberDto> UpdateAsync(
        Guid userId,
        short? roleId,
        UserStatus? status,
        CancellationToken cancellationToken = default)
    {
        if (roleId is null && status is null)
            throw AppErrors.Validation("Nothing to update.");

        var actorId = _currentUser.UserId;

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw AppErrors.NotFound("Member");

        var now = DateTime.UtcNow;

        if (status is not null && status != user.Status)
        {
            if (status is not (UserStatus.Active or UserStatus.Disabled))
                throw new AppException("Status can only be changed to Active or Disabled.", 400, ErrorCode.StatusInvalid);

            if (user.Status == UserStatus.Invited)
                throw AppErrors.Validation("This member has not joined yet. Revoke their invitation instead.");

            if (user.Id == actorId)
                throw AppErrors.Conflict("You cannot change the status of your own account.");

            if (status == UserStatus.Disabled)
                await EnsureAnotherActiveAdminRemainsAsync(user, cancellationToken);

            user.Status = status.Value;
            user.UpdatedAtUtc = now;
        }

        if (roleId is not null)
        {
            if (roleId is not (RoleIds.Admin or RoleIds.Member or RoleIds.ProjectManager))
                throw new AppException("Role must be Admin, Member, or Project Manager.");

            var currentRole = user.UserRoles.FirstOrDefault()
                ?? throw new AppException($"User {user.Id} has no assigned role.", 500);

            if (currentRole.RoleId != roleId)
            {
                if (currentRole.RoleId == RoleIds.Admin)
                    await EnsureAnotherActiveAdminRemainsAsync(user, cancellationToken);

                // RoleId is part of the user_roles primary key, so the row is
                // replaced rather than mutated.
                _db.UserRoles.Remove(currentRole);
                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId.Value,
                    AssignedAtUtc = now,
                    AssignedByUserId = actorId
                });

                // Keep any pending invitation consistent so the invitations
                // list shows the role the invitee will actually get.
                if (user.Status == UserStatus.Invited)
                {
                    var pendingInvitations = await _db.Invitations
                        .Where(i => i.Email == user.Email && i.Status == InvitationStatus.Pending)
                        .ToListAsync(cancellationToken);

                    foreach (var invitation in pendingInvitations)
                    {
                        invitation.RoleId = roleId.Value;
                        invitation.UpdatedAtUtc = now;
                    }
                }

                user.UpdatedAtUtc = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.HourTargets)
            .FirstAsync(u => u.Id == userId, cancellationToken);

        var pendingInvitationsByEmail = await _db.Invitations
            .AsNoTracking()
            .Where(i => i.Email == updated.Email && i.Status == InvitationStatus.Pending)
            .ToDictionaryAsync(i => i.Email, i => i.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return MapMember(updated, pendingInvitationsByEmail);
    }

    /// <summary>
    /// Guards against locking the workspace out: the change is rejected when it
    /// would leave no active admin behind. Only relevant when the user losing
    /// access or the Admin role is an active admin themselves.
    /// </summary>
    private async Task EnsureAnotherActiveAdminRemainsAsync(User user, CancellationToken cancellationToken)
    {
        var isActiveAdmin = user.Status == UserStatus.Active &&
                            user.UserRoles.Any(ur => ur.RoleId == RoleIds.Admin);
        if (!isActiveAdmin)
            return;

        var anotherActiveAdminExists = await _db.UserRoles.AnyAsync(
            ur => ur.RoleId == RoleIds.Admin &&
                  ur.UserId != user.Id &&
                  ur.User.Status == UserStatus.Active,
            cancellationToken);

        if (!anotherActiveAdminExists)
            throw AppErrors.Conflict("At least one active admin is required.");
    }

    internal static MemberDto MapMember(User user, IReadOnlyDictionary<string, Guid>? pendingInvitations = null)
    {
        var role = user.UserRoles.FirstOrDefault()?.Role
            ?? throw new InvalidOperationException($"User {user.Id} has no assigned role.");

        var hourTarget = user.HourTargets.FirstOrDefault();

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
                : null,
            HourTargetMode = hourTarget?.Mode,
            HourTargetHours = hourTarget?.TargetHours
        };
    }
}

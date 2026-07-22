using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IGoogleCodeExchanger _googleCodeExchanger;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly GoogleAuthOptions _googleOptions;

    public AuthService(
        IApplicationDbContext db,
        IGoogleCodeExchanger googleCodeExchanger,
        IJwtTokenService jwtTokenService,
        IOptions<GoogleAuthOptions> googleOptions)
    {
        _db = db;
        _googleCodeExchanger = googleCodeExchanger;
        _jwtTokenService = jwtTokenService;
        _googleOptions = googleOptions.Value;
    }

    public async Task<AuthResult> SignInWithGoogleAsync(string code, CancellationToken cancellationToken = default)
    {
        var googleUser = await _googleCodeExchanger.ExchangeAsync(code, cancellationToken);
        var isFirstRun = !await _db.Users.AnyAsync(cancellationToken);

        if (isFirstRun)
            return await CreateFirstAdminAsync(googleUser, cancellationToken);

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                u => u.GoogleSub == googleUser.Subject || u.Email == googleUser.Email,
                cancellationToken);

        if (user is null)
            throw new AuthException(
                "Access denied. An administrator must invite you before you can sign in.",
                403);

        if (user.Status == UserStatus.Disabled)
            throw new AuthException("This account has been disabled.", 403);

        if (user.Status == UserStatus.Invited)
            await AcceptPendingInvitationAsync(user, cancellationToken);

        await UpdateUserFromGoogleAsync(user, googleUser, cancellationToken);

        return BuildAuthResult(user);
    }

    /// <summary>
    /// First sign-in of an invited user: enforces invitation expiry and marks the
    /// pending invitation as accepted so its link stops resolving. Users without
    /// any invitation rows (pre-invitations data) are let through unchanged; users
    /// whose invitations were all revoked or expired are rejected so a revoke
    /// actually removes access.
    /// </summary>
    private async Task AcceptPendingInvitationAsync(User user, CancellationToken cancellationToken)
    {
        var invitations = await _db.Invitations
            .Where(i => i.Email == user.Email)
            .ToListAsync(cancellationToken);

        if (invitations.Count == 0)
            return;

        var pendingInvitations = invitations
            .Where(i => i.Status == InvitationStatus.Pending)
            .ToList();

        var now = DateTime.UtcNow;
        var current = pendingInvitations
            .Where(i => i.ExpiresAtUtc > now)
            .OrderByDescending(i => i.ExpiresAtUtc)
            .FirstOrDefault();

        if (current is null)
            throw new AuthException(
                pendingInvitations.Count > 0
                    ? "Your invitation has expired. Ask an administrator to send a new one."
                    : "Your invitation is no longer valid. Ask an administrator to send a new one.",
                403);

        foreach (var invitation in pendingInvitations)
        {
            if (invitation == current)
            {
                invitation.Status = InvitationStatus.Accepted;
                invitation.AcceptedAtUtc = now;
                invitation.AcceptedByUserId = user.Id;
            }
            else
            {
                invitation.Status = InvitationStatus.Revoked;
            }

            invitation.UpdatedAtUtc = now;
        }
    }

    public async Task<AuthenticatedUser> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            throw new AuthException("User not found.", 401);

        if (user.Status == UserStatus.Disabled)
            throw new AuthException("This account has been disabled.", 403);

        return MapToAuthenticatedUser(user);
    }

    private async Task<AuthResult> CreateFirstAdminAsync(
        GoogleTokenPayload googleUser,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_googleOptions.AdminEmail) &&
            !string.Equals(googleUser.Email, _googleOptions.AdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthException(
                "Only the configured administrator email may complete initial setup.",
                403);
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = googleUser.Email,
            GoogleSub = googleUser.Subject,
            DisplayName = googleUser.Name,
            AvatarUrl = googleUser.Picture,
            Status = UserStatus.Active,
            EmailVerified = googleUser.EmailVerified,
            LastLoginAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UserRoles =
            [
                new UserRole
                {
                    RoleId = RoleIds.Admin,
                    AssignedAtUtc = now
                }
            ]
        };

        user.AssignInitialHourlyRate(DateOnly.FromDateTime(now));

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == user.Id, cancellationToken);

        return BuildAuthResult(user);
    }

    private async Task UpdateUserFromGoogleAsync(
        User user,
        GoogleTokenPayload googleUser,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        user.GoogleSub ??= googleUser.Subject;
        user.DisplayName = googleUser.Name ?? user.DisplayName;
        user.AvatarUrl = googleUser.Picture ?? user.AvatarUrl;
        user.EmailVerified = googleUser.EmailVerified;
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;

        if (user.Status == UserStatus.Invited)
            user.Status = UserStatus.Active;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private AuthResult BuildAuthResult(User user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var accessToken = _jwtTokenService.CreateAccessToken(user, roles, out var expiresAtUtc);

        return new AuthResult
        {
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAtUtc,
            User = MapToAuthenticatedUser(user, roles)
        };
    }

    private static AuthenticatedUser MapToAuthenticatedUser(User user, IReadOnlyList<string>? roles = null)
    {
        roles ??= user.UserRoles.Select(ur => ur.Role.Name).ToList();

        return new AuthenticatedUser
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Roles = roles
        };
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Invitations;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Members;
using ReeTrack.Infrastructure.Persistence;

namespace ReeTrack.Infrastructure.Invitations;

public class InvitationService : IInvitationService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ICurrentUserService _currentUser;
    private readonly InvitationOptions _invitationOptions;
    private readonly AppOptions _appOptions;
    private readonly string _frontendOrigin;

    public InvitationService(
        AppDbContext db,
        IEmailSender emailSender,
        ICurrentUserService currentUser,
        IOptions<InvitationOptions> invitationOptions,
        IOptions<AppOptions> appOptions,
        IConfiguration configuration)
    {
        _db = db;
        _emailSender = emailSender;
        _currentUser = currentUser;
        _invitationOptions = invitationOptions.Value;
        _appOptions = appOptions.Value;
        _frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
    }

    public async Task<CreateInvitationResult> CreateAsync(
        string email,
        short roleId,
        CancellationToken cancellationToken = default)
    {
        var adminId = RequireAdminId();
        var normalizedEmail = InvitationTokenHelper.NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
            throw new AppException("A valid email address is required.");

        if (roleId is not (RoleIds.Admin or RoleIds.Member))
            throw new AppException("Role must be Admin or Member.");

        var role = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
            ?? throw new AppException("Role must be Admin or Member.");

        var existingUser = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (existingUser?.Status == UserStatus.Active)
            throw new AppException("A user with this email already has access.", 409);

        var inviter = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == adminId, cancellationToken)
            ?? throw new AppException("Current user was not found.", 401);

        var inviterName = inviter.DisplayName ?? inviter.Email;
        var rawToken = InvitationTokenHelper.GenerateToken();
        var tokenHash = InvitationTokenHelper.HashToken(rawToken);
        var now = DateTime.UtcNow;
        var expiresAt = now.AddDays(_invitationOptions.ExpiryDays);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        User user;
        Invitation invitation;

        try
        {
            await RevokePendingInvitationsAsync(normalizedEmail, now, cancellationToken);

            if (existingUser is null)
            {
                user = new User
                {
                    Email = normalizedEmail,
                    DisplayName = InvitationTokenHelper.DisplayNameFromEmail(normalizedEmail),
                    Status = UserStatus.Invited,
                    EmailVerified = false,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                _db.Users.Add(user);
            }
            else
            {
                user = existingUser;
                user.Status = UserStatus.Invited;
                user.UpdatedAtUtc = now;
            }

            await _db.SaveChangesAsync(cancellationToken);

            var userRole = await _db.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == user.Id, cancellationToken);

            if (userRole is null)
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId,
                    AssignedAtUtc = now,
                    AssignedByUserId = adminId
                });
            }
            else if (userRole.RoleId != roleId)
            {
                userRole.RoleId = roleId;
                userRole.AssignedAtUtc = now;
                userRole.AssignedByUserId = adminId;
            }

            invitation = new Invitation
            {
                Email = normalizedEmail,
                RoleId = roleId,
                TokenHash = tokenHash,
                Status = InvitationStatus.Pending,
                ExpiresAtUtc = expiresAt,
                InvitedByUserId = adminId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.Invitations.Add(invitation);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await SendInviteEmailOrThrowAsync(
            normalizedEmail,
            rawToken,
            inviterName,
            role.Name,
            cancellationToken);

        user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == user.Id, cancellationToken);

        var member = MemberService.MapMember(user, new Dictionary<string, Guid>
        {
            [normalizedEmail] = invitation.Id
        });

        return new CreateInvitationResult
        {
            Member = member,
            Invitation = MapInvitation(invitation, role.Name)
        };
    }

    public async Task<InvitationDto> ResendAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var adminId = RequireAdminId();

        var existingInvitation = await _db.Invitations
            .Include(i => i.Role)
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken)
            ?? throw new AppException("Invitation was not found.", 404);

        if (existingInvitation.Status != InvitationStatus.Pending)
            throw new AppException("Only pending invitations can be resent.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == existingInvitation.Email, cancellationToken)
            ?? throw new AppException("Invited user was not found.", 404);

        if (user.Status != UserStatus.Invited)
            throw new AppException("This user is no longer in an invited state.");

        var inviter = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == adminId, cancellationToken)
            ?? throw new AppException("Current user was not found.", 401);

        var inviterName = inviter.DisplayName ?? inviter.Email;
        var rawToken = InvitationTokenHelper.GenerateToken();
        var tokenHash = InvitationTokenHelper.HashToken(rawToken);
        var now = DateTime.UtcNow;
        var expiresAt = now.AddDays(_invitationOptions.ExpiryDays);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        Invitation invitation;

        try
        {
            existingInvitation.Status = InvitationStatus.Revoked;
            existingInvitation.UpdatedAtUtc = now;

            invitation = new Invitation
            {
                Email = existingInvitation.Email,
                RoleId = existingInvitation.RoleId,
                TokenHash = tokenHash,
                Status = InvitationStatus.Pending,
                ExpiresAtUtc = expiresAt,
                InvitedByUserId = adminId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.Invitations.Add(invitation);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await SendInviteEmailOrThrowAsync(
            existingInvitation.Email,
            rawToken,
            inviterName,
            existingInvitation.Role.Name,
            cancellationToken);

        return MapInvitation(invitation, existingInvitation.Role.Name);
    }

    public async Task<InvitationPreviewDto> GetPreviewAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new AppException("Invitation token is required.", 404);

        var tokenHash = InvitationTokenHelper.HashToken(token);
        var invitation = await _db.Invitations
            .AsNoTracking()
            .Include(i => i.Role)
            .Include(i => i.InvitedByUser)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null ||
            invitation.Status != InvitationStatus.Pending ||
            invitation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new AppException("Invitation was not found.", 404);
        }

        var inviterName = invitation.InvitedByUser.DisplayName ?? invitation.InvitedByUser.Email;

        return new InvitationPreviewDto
        {
            InvitedEmail = invitation.Email,
            InviterName = inviterName,
            Role = invitation.Role.Name,
            AppName = _appOptions.Name
        };
    }

    private Guid RequireAdminId()
    {
        if (_currentUser.UserId is not Guid adminId)
            throw new AppException("Authentication is required.", 401);

        return adminId;
    }

    private async Task RevokePendingInvitationsAsync(
        string normalizedEmail,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var pendingInvitations = await _db.Invitations
            .Where(i => i.Email == normalizedEmail && i.Status == InvitationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var invitation in pendingInvitations)
        {
            invitation.Status = InvitationStatus.Revoked;
            invitation.UpdatedAtUtc = now;
        }
    }

    /// <summary>
    /// Sends the invite email after the invitation has been committed. Delivery failures
    /// surface as a 502 so the admin knows the invitation exists and can use "resend".
    /// </summary>
    private async Task SendInviteEmailOrThrowAsync(
        string toEmail,
        string rawToken,
        string inviterName,
        string roleName,
        CancellationToken cancellationToken)
    {
        try
        {
            var inviteUrl = BuildInviteUrl(rawToken);
            await _emailSender.SendInviteEmailAsync(
                toEmail,
                inviteUrl,
                inviterName,
                roleName,
                _appOptions.Name,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new AppException(
                "The invitation was saved, but the invite email could not be sent. Use resend to try again.",
                502);
        }
    }

    private string BuildInviteUrl(string rawToken) =>
        $"{_frontendOrigin.TrimEnd('/')}/signin?token={Uri.EscapeDataString(rawToken)}";

    private static InvitationDto MapInvitation(Invitation invitation, string roleName) =>
        new()
        {
            Id = invitation.Id,
            Email = invitation.Email,
            Role = roleName,
            RoleId = invitation.RoleId,
            Status = invitation.Status,
            ExpiresAtUtc = invitation.ExpiresAtUtc,
            InvitedByUserId = invitation.InvitedByUserId
        };
}

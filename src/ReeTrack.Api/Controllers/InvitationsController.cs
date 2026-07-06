using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/invitations")]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<CreateInvitationResponse>> Create(
        [FromBody] CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _invitationService.CreateAsync(request.Email, request.RoleId, cancellationToken);

            return Ok(new CreateInvitationResponse
            {
                Member = MapMember(result.Member),
                Invitation = MapInvitation(result.Invitation)
            });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/resend")]
    public async Task<ActionResult<InvitationResponse>> Resend(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var invitation = await _invitationService.ResendAsync(id, cancellationToken);
            return Ok(MapInvitation(invitation));
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("preview")]
    public async Task<ActionResult<InvitationPreviewResponse>> Preview(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var preview = await _invitationService.GetPreviewAsync(token, cancellationToken);

            return Ok(new InvitationPreviewResponse
            {
                InvitedEmail = preview.InvitedEmail,
                InviterName = preview.InviterName,
                Role = preview.Role,
                AppName = preview.AppName
            });
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    private static MemberResponse MapMember(Application.Common.Models.MemberDto member) =>
        new()
        {
            Id = member.Id,
            Email = member.Email,
            DisplayName = member.DisplayName,
            AvatarUrl = member.AvatarUrl,
            Role = member.Role,
            RoleId = member.RoleId,
            Status = member.Status.ToString(),
            EmailVerified = member.EmailVerified,
            LastLoginAtUtc = member.LastLoginAtUtc,
            PendingInvitationId = member.PendingInvitationId
        };

    private static InvitationResponse MapInvitation(Application.Common.Models.InvitationDto invitation) =>
        new()
        {
            Id = invitation.Id,
            Email = invitation.Email,
            Role = invitation.Role,
            RoleId = invitation.RoleId,
            Status = invitation.Status.ToString(),
            ExpiresAtUtc = invitation.ExpiresAtUtc,
            InvitedByUserId = invitation.InvitedByUserId
        };
}

public sealed class CreateInvitationRequest
{
    public string Email { get; set; } = string.Empty;
    public short RoleId { get; set; }
}

public sealed class CreateInvitationResponse
{
    public required MemberResponse Member { get; init; }
    public required InvitationResponse Invitation { get; init; }
}

public sealed class InvitationResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required short RoleId { get; init; }
    public required string Status { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required Guid InvitedByUserId { get; init; }
}

public sealed class InvitationPreviewResponse
{
    public required string InvitedEmail { get; init; }
    public required string InviterName { get; init; }
    public required string Role { get; init; }
    public required string AppName { get; init; }
}

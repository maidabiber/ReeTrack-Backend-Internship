using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Constants;
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

    [Authorize(Policy = Permissions.Policies.InvitationsManage)]
    [HttpPost]
    public async Task<ActionResult<CreateInvitationResponse>> Create(
        [FromBody] CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _invitationService.CreateAsync(request.Email, request.RoleId, cancellationToken);

        return Ok(new CreateInvitationResponse
        {
            Member = MapMember(result.Member),
            Invitation = MapInvitation(result.Invitation)
        });
    }

    [Authorize(Policy = Permissions.Policies.InvitationsManage)]
    [HttpPost("batch")]
    public async Task<ActionResult<BatchInvitationResponse>> CreateBatch(
        [FromBody] BatchInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var results = await _invitationService.CreateManyAsync(
            request.Emails ?? [],
            request.RoleId,
            cancellationToken);

        return Ok(new BatchInvitationResponse
        {
            Results = results.Select(row => new BatchInvitationRowResponse
            {
                Email = row.Email,
                Status = row.Status.ToString(),
                Message = row.Message,
                Member = row.Member is null ? null : MapMember(row.Member)
            }).ToList()
        });
    }

    [Authorize(Policy = Permissions.Policies.InvitationsManage)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvitationListItemResponse>>> List(
        CancellationToken cancellationToken)
    {
        var invitations = await _invitationService.ListAsync(cancellationToken);

        return Ok(invitations.Select(invitation => new InvitationListItemResponse
        {
            Id = invitation.Id,
            Email = invitation.Email,
            Role = invitation.Role,
            RoleId = invitation.RoleId,
            Status = invitation.Status,
            CreatedAtUtc = invitation.CreatedAtUtc,
            ExpiresAtUtc = invitation.ExpiresAtUtc,
            InvitedByName = invitation.InvitedByName,
            AcceptedAtUtc = invitation.AcceptedAtUtc
        }).ToList());
    }

    [Authorize(Policy = Permissions.Policies.InvitationsManage)]
    [HttpPost("{id:guid}/revoke")]
    public async Task<ActionResult<RevokeInvitationResponse>> Revoke(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _invitationService.RevokeAsync(id, cancellationToken);

        return Ok(new RevokeInvitationResponse
        {
            Invitation = MapInvitation(result.Invitation),
            RemovedUserId = result.RemovedUserId
        });
    }

    [Authorize(Policy = Permissions.Policies.InvitationsManage)]
    [HttpPost("{id:guid}/resend")]
    public async Task<ActionResult<InvitationResponse>> Resend(
        Guid id,
        CancellationToken cancellationToken)
    {
        var invitation = await _invitationService.ResendAsync(id, cancellationToken);
        return Ok(MapInvitation(invitation));
    }

    [Authorize(Policy = Permissions.Policies.InvitationsManage)]
    [HttpGet("allowed-domains")]
    public ActionResult<AllowedDomainsResponse> AllowedDomains() =>
        Ok(new AllowedDomainsResponse { Domains = _invitationService.GetAllowedDomains() });

    [AllowAnonymous]
    [HttpGet("preview")]
    public async Task<ActionResult<InvitationPreviewResponse>> Preview(
        [FromQuery] string token,
        CancellationToken cancellationToken)
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
            PendingInvitationId = member.PendingInvitationId,
            HourTargetMode = member.HourTargetMode?.ToString(),
            HourTargetHours = member.HourTargetHours
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

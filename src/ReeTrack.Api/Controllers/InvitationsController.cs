using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        var result = await _invitationService.CreateAsync(request.Email, request.RoleId, cancellationToken);

        return Ok(new CreateInvitationResponse
        {
            Member = MapMember(result.Member),
            Invitation = MapInvitation(result.Invitation)
        });
    }

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/resend")]
    public async Task<ActionResult<InvitationResponse>> Resend(
        Guid id,
        CancellationToken cancellationToken)
    {
        var invitation = await _invitationService.ResendAsync(id, cancellationToken);
        return Ok(MapInvitation(invitation));
    }

    [Authorize(Roles = "Admin")]
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

public sealed class BatchInvitationRequest
{
    public List<string>? Emails { get; set; }
    public short RoleId { get; set; }
}

public sealed class BatchInvitationResponse
{
    public required IReadOnlyList<BatchInvitationRowResponse> Results { get; init; }
}

public sealed class BatchInvitationRowResponse
{
    public required string Email { get; init; }
    public required string Status { get; init; }
    public string? Message { get; init; }
    public MemberResponse? Member { get; init; }
}

public sealed class InvitationListItemResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required short RoleId { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required string InvitedByName { get; init; }
    public DateTime? AcceptedAtUtc { get; init; }
}

public sealed class RevokeInvitationResponse
{
    public required InvitationResponse Invitation { get; init; }
    public Guid? RemovedUserId { get; init; }
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

public sealed class AllowedDomainsResponse
{
    public required IReadOnlyList<string> Domains { get; init; }
}

public sealed class InvitationPreviewResponse
{
    public required string InvitedEmail { get; init; }
    public required string InviterName { get; init; }
    public required string Role { get; init; }
    public required string AppName { get; init; }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/members")]
[Authorize(Roles = "Admin")]
public class MembersController : ControllerBase
{
    private readonly IMemberService _memberService;

    public MembersController(IMemberService memberService)
    {
        _memberService = memberService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberResponse>>> List(CancellationToken cancellationToken)
    {
        var members = await _memberService.ListAsync(cancellationToken);

        return Ok(members.Select(MapMember).ToList());
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<MemberResponse>> Update(
        Guid id,
        [FromBody] UpdateMemberRequest request,
        CancellationToken cancellationToken)
    {
        UserStatus? status = null;
        if (request.Status is not null)
        {
            if (!Enum.TryParse<UserStatus>(request.Status, ignoreCase: true, out var parsed))
                return BadRequest(new { message = "Status must be Active or Disabled." });
            status = parsed;
        }

        var member = await _memberService.UpdateAsync(id, request.RoleId, status, cancellationToken);
        return Ok(MapMember(member));
    }

    internal static MemberResponse MapMember(MemberDto member) =>
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
}

public sealed class UpdateMemberRequest
{
    public short? RoleId { get; set; }
    /// <summary>"Active" or "Disabled".</summary>
    public string? Status { get; set; }
}

public sealed class MemberResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required string Role { get; init; }
    public required short RoleId { get; init; }
    public required string Status { get; init; }
    public required bool EmailVerified { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
    public Guid? PendingInvitationId { get; init; }
}

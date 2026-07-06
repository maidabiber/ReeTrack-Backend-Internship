using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Interfaces;

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

        return Ok(members.Select(member => new MemberResponse
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
        }).ToList());
    }
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

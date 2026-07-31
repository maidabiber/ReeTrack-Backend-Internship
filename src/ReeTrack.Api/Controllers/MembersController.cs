using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Exceptions;
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
    public async Task<ActionResult<PagedResult<MemberResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _memberService.ListAsync(new MemberListQuery
        {
            Page = page,
            PageSize = pageSize,
            Q = q
        }, cancellationToken);

        return Ok(new PagedResult<MemberResponse>
        {
            Items = result.Items.Select(MapMember).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
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
                throw new AppException("Status must be Active or Disabled.", 400, ErrorCode.StatusInvalid);
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
            PendingInvitationId = member.PendingInvitationId,
            HourTargetMode = member.HourTargetMode?.ToString(),
            HourTargetHours = member.HourTargetHours
        };
}

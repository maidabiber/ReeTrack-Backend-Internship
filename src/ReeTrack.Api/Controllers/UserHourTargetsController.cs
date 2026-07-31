using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/members/{userId:guid}/hour-target")]
[Authorize(Roles = "Admin")]
public class UserHourTargetsController : ControllerBase
{
    private readonly IUserHourTargetService _hourTargetService;

    public UserHourTargetsController(IUserHourTargetService hourTargetService)
    {
        _hourTargetService = hourTargetService;
    }

    [HttpGet]
    public async Task<ActionResult<UserHourTargetResponse?>> Get(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var target = await _hourTargetService.GetOverrideAsync(userId, cancellationToken);
        if (target is null)
            return Ok(null);

        return Ok(Map(target));
    }

    [HttpPut]
    public async Task<ActionResult<UserHourTargetResponse>> Upsert(
        Guid userId,
        [FromBody] HourTargetPayload request,
        CancellationToken cancellationToken)
    {
        if (!HourTargetSettingsController.TryParseMode(request.Mode, out var mode))
            return BadRequest(new { message = "Mode must be Daily or Weekly." });

        var target = await _hourTargetService.UpsertOverrideAsync(
            userId,
            mode,
            request.TargetHours,
            cancellationToken);

        return Ok(Map(target));
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(Guid userId, CancellationToken cancellationToken)
    {
        await _hourTargetService.ClearOverrideAsync(userId, cancellationToken);
        return NoContent();
    }

    private static UserHourTargetResponse Map(UserHourTargetDto target) =>
        new()
        {
            UserId = target.UserId,
            Mode = target.Mode.ToString(),
            TargetHours = target.TargetHours
        };
}

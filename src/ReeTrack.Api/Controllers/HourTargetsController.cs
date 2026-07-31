using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/hour-targets")]
[Authorize]
public class HourTargetsController : ControllerBase
{
    private readonly IUserHourTargetService _hourTargetService;
    private readonly ICurrentUserService _currentUser;

    public HourTargetsController(
        IUserHourTargetService hourTargetService,
        ICurrentUserService currentUser)
    {
        _hourTargetService = hourTargetService;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<ActionResult<EffectiveHourTargetResponse>> GetMine(CancellationToken cancellationToken)
    {
        var target = await _hourTargetService.GetEffectiveForUserAsync(
            _currentUser.UserId,
            cancellationToken);

        return Ok(Map(target));
    }

    private static EffectiveHourTargetResponse Map(EffectiveHourTargetDto target) =>
        new()
        {
            Mode = target.Mode.ToString(),
            TargetHours = target.TargetHours,
            IsOverride = target.IsOverride,
            IsWorkdayToday = target.IsWorkdayToday,
            HolidayDates = target.HolidayDates
        };
}

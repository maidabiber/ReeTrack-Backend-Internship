using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/rate-multiplier-settings")]
[Authorize(Roles = "Admin")]
public class RateMultiplierSettingsController : ControllerBase
{
    private readonly IRateMultiplierSettingsService _settingsService;

    public RateMultiplierSettingsController(IRateMultiplierSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<ActionResult<RateMultiplierSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);
        return Ok(Map(settings));
    }

    [HttpPut]
    public async Task<ActionResult<RateMultiplierSettingsResponse>> Update(
        [FromBody] UpdateRateMultiplierSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsService.UpdateAsync(
            new RateMultiplierSettingsDto
            {
                WeekendPremium = request.WeekendPremium,
                HolidayPremium = request.HolidayPremium,
                OvertimePremium = request.OvertimePremium,
                WeeklyOvertimeThresholdHours = request.WeeklyOvertimeThresholdHours
            },
            cancellationToken);

        return Ok(Map(settings));
    }

    private static RateMultiplierSettingsResponse Map(RateMultiplierSettingsDto settings) =>
        new()
        {
            WeekendPremium = settings.WeekendPremium,
            HolidayPremium = settings.HolidayPremium,
            OvertimePremium = settings.OvertimePremium,
            WeeklyOvertimeThresholdHours = settings.WeeklyOvertimeThresholdHours
        };
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/hour-target-settings")]
[Authorize(Roles = "Admin")]
public class HourTargetSettingsController : ControllerBase
{
    private readonly IHourTargetSettingsService _settingsService;

    public HourTargetSettingsController(IHourTargetSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<ActionResult<HourTargetPayload>> Get(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);
        return Ok(Map(settings));
    }

    [HttpPut]
    public async Task<ActionResult<HourTargetPayload>> Update(
        [FromBody] HourTargetPayload request,
        CancellationToken cancellationToken)
    {
        if (!TryParseMode(request.Mode, out var mode))
            return BadRequest(new { message = "Mode must be Daily or Weekly." });

        var settings = await _settingsService.UpdateAsync(
            new HourTargetSettingsDto
            {
                Mode = mode,
                TargetHours = request.TargetHours
            },
            cancellationToken);

        return Ok(Map(settings));
    }

    internal static bool TryParseMode(string? value, out HourTargetMode mode) =>
        Enum.TryParse(value, ignoreCase: true, out mode) && Enum.IsDefined(mode);

    private static HourTargetPayload Map(HourTargetSettingsDto settings) =>
        new()
        {
            Mode = settings.Mode.ToString(),
            TargetHours = settings.TargetHours
        };
}

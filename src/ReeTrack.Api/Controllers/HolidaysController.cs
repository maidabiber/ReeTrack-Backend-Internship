using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/holidays")]
[Authorize]
public class HolidaysController : ControllerBase
{
    private readonly IHolidayService _holidayService;

    public HolidaysController(IHolidayService holidayService)
    {
        _holidayService = holidayService;
    }

    [HttpGet("calendars")]
    [Authorize(Policy = Permissions.Policies.HolidaysManage)]
    public async Task<ActionResult<IReadOnlyList<HolidayCalendarResponse>>> ListCalendars(
        CancellationToken cancellationToken)
    {
        var calendars = await _holidayService.ListCalendarsAsync(cancellationToken);
        return Ok(calendars.Select(c => new HolidayCalendarResponse
        {
            CountryCode = c.CountryCode,
            Name = c.Name
        }).ToList());
    }

    [HttpGet("settings")]
    [Authorize(Policy = Permissions.Policies.HolidaysManage)]
    public async Task<ActionResult<HolidayCalendarSettingsResponse>> GetSettings(
        CancellationToken cancellationToken)
    {
        var settings = await _holidayService.GetSettingsAsync(cancellationToken);
        return Ok(new HolidayCalendarSettingsResponse { CountryCode = settings.CountryCode });
    }

    [HttpPut("settings")]
    [Authorize(Policy = Permissions.Policies.HolidaysManage)]
    public async Task<ActionResult<HolidayCalendarSettingsResponse>> UpdateSettings(
        [FromBody] UpdateHolidayCalendarSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _holidayService.UpdateSettingsAsync(request.CountryCode, cancellationToken);
        return Ok(new HolidayCalendarSettingsResponse { CountryCode = settings.CountryCode });
    }

    [HttpPost("sync")]
    [Authorize(Policy = Permissions.Policies.HolidaysManage)]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        await _holidayService.SyncAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HolidayResponse>>> List(CancellationToken cancellationToken)
    {
        var holidays = await _holidayService.ListAsync(cancellationToken);
        return Ok(holidays.Select(Map).ToList());
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Policies.HolidaysManage)]
    public async Task<ActionResult<HolidayResponse>> Create(
        [FromBody] CreateCustomHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var holiday = await _holidayService.CreateCustomAsync(
            new CreateCustomHolidayRequestDto
            {
                Date = request.Date,
                Name = request.Name
            },
            cancellationToken);

        return Ok(Map(holiday));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.Policies.HolidaysManage)]
    public async Task<ActionResult<HolidayResponse>> SetActive(
        Guid id,
        [FromBody] UpdateHolidayActiveRequest request,
        CancellationToken cancellationToken)
    {
        var holiday = await _holidayService.SetActiveAsync(id, request.IsActive, cancellationToken);
        return Ok(Map(holiday));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Policies.HolidaysManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _holidayService.DeleteCustomAsync(id, cancellationToken);
        return NoContent();
    }

    private static HolidayResponse Map(HolidayDto holiday) =>
        new()
        {
            Id = holiday.Id,
            Date = holiday.Date,
            Name = holiday.Name,
            IsActive = holiday.IsActive,
            Source = holiday.Source,
            CountryCode = holiday.CountryCode
        };
}

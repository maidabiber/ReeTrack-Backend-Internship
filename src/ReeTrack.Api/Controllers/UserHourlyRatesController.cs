using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/members/{userId:guid}/hourly-rates")]
[Authorize]
public class UserHourlyRatesController : ControllerBase
{
    private readonly IUserHourlyRateService _hourlyRateService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissions;

    public UserHourlyRatesController(
        IUserHourlyRateService hourlyRateService,
        ICurrentUserService currentUser,
        IPermissionService permissions)
    {
        _hourlyRateService = hourlyRateService;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserHourlyRateResponse>>> List(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!CanViewHourlyRates(userId))
            return Forbid();

        var rates = await _hourlyRateService.ListByUserAsync(userId, cancellationToken);
        return Ok(rates.Select(Map).ToList());
    }

    [HttpGet("current")]
    public async Task<ActionResult<UserHourlyRateResponse>> GetCurrent(
        Guid userId,
        [FromQuery] DateOnly? onDate,
        CancellationToken cancellationToken)
    {
        if (!CanViewHourlyRates(userId))
            return Forbid();

        var rate = await _hourlyRateService.GetCurrentAsync(userId, onDate, cancellationToken);
        return Ok(Map(rate));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Policies.BillableRatesManage)]
    public async Task<ActionResult<UserHourlyRateResponse>> Change(
        Guid userId,
        [FromBody] ChangeUserHourlyRateRequest request,
        CancellationToken cancellationToken)
    {
        var input = new ChangeUserHourlyRateInput
        {
            HourlyRate = request.HourlyRate,
            ValidFrom = request.ValidFrom,
            CurrencyCode = request.CurrencyCode
        };

        var rate = await _hourlyRateService.ChangeAsync(userId, input, cancellationToken);
        return Ok(Map(rate));
    }

    [HttpPatch("{rateId:guid}")]
    [Authorize(Policy = Permissions.Policies.BillableRatesManage)]
    public async Task<ActionResult<UserHourlyRateResponse>> Correct(
        Guid userId,
        Guid rateId,
        [FromBody] CorrectUserHourlyRateRequest request,
        CancellationToken cancellationToken)
    {
        var input = new CorrectUserHourlyRateInput
        {
            HourlyRate = request.HourlyRate,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            CurrencyCode = request.CurrencyCode
        };

        var rate = await _hourlyRateService.CorrectAsync(userId, rateId, input, cancellationToken);
        return Ok(Map(rate));
    }

    private bool CanViewHourlyRates(Guid userId) =>
        _currentUser.UserId == userId ||
        _permissions.HasPermission(Permissions.BillableRatesManage);

    internal static UserHourlyRateResponse Map(UserHourlyRateDto rate) =>
        new()
        {
            Id = rate.Id,
            UserId = rate.UserId,
            HourlyRate = rate.HourlyRate,
            CurrencyCode = rate.CurrencyCode,
            ValidFrom = rate.ValidFrom,
            ValidTo = rate.ValidTo
        };
}

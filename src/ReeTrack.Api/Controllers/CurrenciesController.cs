using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/currencies")]
[Authorize]
public class CurrenciesController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public CurrenciesController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    [HttpGet]
    public async Task<ActionResult<CurrenciesResponse>> List(CancellationToken cancellationToken)
    {
        var currencies = await _currencyService.ListActiveAsync(cancellationToken);

        return Ok(new CurrenciesResponse
        {
            Items = currencies
                .Select(c => new CurrencyResponse
                {
                    Code = c.Code,
                    Name = c.Name
                })
                .ToList()
        });
    }
}

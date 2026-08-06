using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/shared")]
public sealed class SharedReportController : ControllerBase
{
    private readonly IReportShareService _shareService;

    public SharedReportController(IReportShareService shareService)
    {
        _shareService = shareService;
    }

    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReport(string token, CancellationToken ct)
    {
        var report = await _shareService.GetSharedReportAsync(token, ct);
        return Ok(report);
    }
}

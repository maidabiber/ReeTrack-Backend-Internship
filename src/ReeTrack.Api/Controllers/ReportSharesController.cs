using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/report-shares")]
public sealed class ReportSharesController : ControllerBase
{
    private readonly IReportShareService _shareService;

    public ReportSharesController(IReportShareService shareService)
    {
        _shareService = shareService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateShareLinkRequest request,
        CancellationToken ct)
    {
        var id = await _shareService.GenerateLinkAsync(request, ct);
        var links = await _shareService.FetchLinksAsync(request.ReportType, ct);
        var link = links.First(l => l.Id == id);
        return CreatedAtAction(nameof(GetShareLinks), new { reportType = request.ReportType }, link);
    }

    [HttpGet("{reportType}")]
    public async Task<IActionResult> GetShareLinks(
        ReportShareReportType reportType,
        CancellationToken ct)
    {
        var links = await _shareService.FetchLinksAsync(reportType, ct);
        return Ok(links);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _shareService.RemoveLinkAsync(id, ct);
        return NoContent();
    }
}

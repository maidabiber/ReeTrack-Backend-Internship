using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly ISetupService _setupService;

    public SetupController(ISetupService setupService)
    {
        _setupService = setupService;
    }

    [AllowAnonymous]
    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _setupService.GetStatusAsync(cancellationToken);

        return Ok(new SetupStatusResponse
        {
            IsFirstRun = status.IsFirstRun,
            RequiresAdminLogin = status.RequiresAdminLogin
        });
    }
}

public sealed class SetupStatusResponse
{
    public required bool IsFirstRun { get; init; }
    public required bool RequiresAdminLogin { get; init; }
}

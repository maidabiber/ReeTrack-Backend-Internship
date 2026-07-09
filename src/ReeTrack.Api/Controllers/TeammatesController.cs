using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/teammates")]
[Authorize]
public class TeammatesController : ControllerBase
{
    private readonly ITeammateService _teammateService;

    public TeammatesController(ITeammateService teammateService)
    {
        _teammateService = teammateService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeammateResponse>>> List(CancellationToken cancellationToken)
    {
        var teammates = await _teammateService.ListAsync(cancellationToken);
        return Ok(teammates.Select(MapTeammate).ToList());
    }

    internal static TeammateResponse MapTeammate(TeammateDto teammate) =>
        new()
        {
            Id = teammate.Id,
            Email = teammate.Email,
            DisplayName = teammate.DisplayName
        };
}

public sealed class TeammateResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
}

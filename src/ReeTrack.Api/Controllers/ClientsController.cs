using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

// Trust-based domain: every authenticated user may create/edit/delete clients
// (no Admin role gate on mutations). Changes are captured by the audit trail
// and deletes are soft-deletes guarded against existing projects.
[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;

    public ClientsController(IClientService clientService)
    {
        _clientService = clientService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientResponse>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var clients = await _clientService.ListAsync(status, cancellationToken);
        return Ok(clients.Select(MapClient).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ClientResponse>> Create(
        [FromBody] CreateClientRequest? request,
        CancellationToken cancellationToken)
    {
        var client = await _clientService.CreateAsync(request?.Name, cancellationToken);
        return Ok(MapClient(client));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ClientResponse>> Update(
        Guid id,
        [FromBody] UpdateClientRequest? request,
        CancellationToken cancellationToken)
    {
        var client = await _clientService.UpdateAsync(
            id,
            request?.Name,
            request?.IsActive,
            cancellationToken);

        return Ok(MapClient(client));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _clientService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    internal static ClientResponse MapClient(ClientDto client) =>
        new()
        {
            Id = client.Id,
            Name = client.Name,
            IsActive = client.IsActive,
            ProjectCount = client.ProjectCount,
            CreatedAtUtc = client.CreatedAtUtc
        };
}

public sealed class CreateClientRequest
{
    public string? Name { get; set; }
}

public sealed class UpdateClientRequest
{
    public string? Name { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class ClientResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required int ProjectCount { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

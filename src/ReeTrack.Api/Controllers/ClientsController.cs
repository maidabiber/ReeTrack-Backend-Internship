using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
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

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<ClientLookupDto>>> Search(
        [FromQuery] string q,
        [FromQuery] int max = 8,
        CancellationToken cancellationToken = default)
    {
        max = Math.Clamp(max, 1, 20);
        return Ok(await _clientService.SearchAsync(q, max, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _clientService.ListAsync(new ClientListQuery
        {
            Status = status,
            Page = page,
            PageSize = pageSize,
            Q = q
        }, cancellationToken);

        return Ok(new PagedResult<ClientResponse>
        {
            Items = result.Items.Select(MapClient).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
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

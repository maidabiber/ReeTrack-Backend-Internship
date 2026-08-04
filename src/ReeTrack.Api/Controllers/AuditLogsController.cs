using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = Permissions.Policies.AuditLogsView)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _auditLogService.ListAsync(new AuditLogQuery
        {
            Page = page,
            PageSize = pageSize,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = actorUserId,
            Action = action,
            FromUtc = fromUtc,
            ToUtc = toUtc
        }, cancellationToken);

        return Ok(result);
    }
}

using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Persistence;

namespace ReeTrack.Infrastructure.Auditing;

public class AuditLogService : IAuditLogService
{
    // Concrete context on purpose: AuditLog is kept off IApplicationDbContext so
    // application code cannot write audit rows (same precedent as InvitationService).
    private readonly AppDbContext _db;

    public AuditLogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AuditLogDto>> ListAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var logs = _db.Set<AuditLog>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityType))
            logs = logs.Where(l => l.EntityType == query.EntityType);

        if (!string.IsNullOrWhiteSpace(query.EntityId))
            logs = logs.Where(l => l.EntityId == query.EntityId);

        if (query.ActorUserId is Guid actorId)
            logs = logs.Where(l => l.ActorUserId == actorId);

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            if (!Enum.TryParse<AuditAction>(query.Action, ignoreCase: true, out var action))
                throw new AppException("Action must be Created, Updated, Deleted or Restored.");
            logs = logs.Where(l => l.Action == action);
        }

        if (query.FromUtc is DateTime from)
            logs = logs.Where(l => l.OccurredAtUtc >= from);

        if (query.ToUtc is DateTime to)
            logs = logs.Where(l => l.OccurredAtUtc <= to);

        var totalCount = await logs.CountAsync(cancellationToken);

        var items = await logs
            .OrderByDescending(l => l.OccurredAtUtc)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var actorIds = items
            .Where(l => l.ActorUserId is not null)
            .Select(l => l.ActorUserId!.Value)
            .Distinct()
            .ToList();

        var actorEmails = await _db.Users
            .AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var dtos = items.Select(l => new AuditLogDto
        {
            Id = l.Id,
            EntityType = l.EntityType,
            EntityId = l.EntityId,
            Action = l.Action.ToString(),
            ActorUserId = l.ActorUserId,
            ActorEmail = l.ActorUserId is Guid id && actorEmails.TryGetValue(id, out var email)
                ? email
                : null,
            OldValues = l.OldValuesJson,
            NewValues = l.NewValuesJson,
            OccurredAtUtc = l.OccurredAtUtc
        }).ToList();

        return new PagedResult<AuditLogDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}

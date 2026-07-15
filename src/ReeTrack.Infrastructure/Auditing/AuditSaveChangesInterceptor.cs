using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ReeTrack.Application.Common.Auditing;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Common;

namespace ReeTrack.Infrastructure.Auditing;

/// <summary>
/// Writes an <see cref="AuditLog"/> row for create/update/delete of <see cref="IAuditable"/>
/// entities, in the same SaveChanges (and therefore the same transaction) as the change itself.
/// Also centralizes BaseEntity Id/timestamp stamping so services no longer need to.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ProcessSave(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ProcessSave(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ProcessSave(DbContext? context)
    {
        if (context is null)
            return;

        context.ChangeTracker.DetectChanges();

        var now = DateTime.UtcNow;
        Guid? actorId = _currentUser.IsAuthenticated ? _currentUser.UserId : null;
        var auditLogs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog)
            {
                if (entry.State is EntityState.Modified or EntityState.Deleted)
                    throw new InvalidOperationException("Audit logs are append-only.");
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            StampBaseEntity(entry, now);

            if (entry.Entity is not IAuditable)
                continue;

            var auditLog = BuildAuditLog(entry, actorId, now);
            if (auditLog is not null)
                auditLogs.Add(auditLog);
        }

        if (auditLogs.Count > 0)
            context.Set<AuditLog>().AddRange(auditLogs);
    }

    private static void StampBaseEntity(EntityEntry entry, DateTime now)
    {
        if (entry.Entity is not BaseEntity baseEntity)
            return;

        if (entry.State == EntityState.Added)
        {
            // Client-assigned key so the audit row can reference it before the DB
            // generates one; the gen_random_uuid() default remains as a fallback.
            if (baseEntity.Id == Guid.Empty)
                entry.Property(nameof(BaseEntity.Id)).CurrentValue = Guid.CreateVersion7();

            // Fill-if-default keeps explicitly chosen timestamps (seeding, tests) intact.
            if (baseEntity.CreatedAtUtc == default)
                entry.Property(nameof(BaseEntity.CreatedAtUtc)).CurrentValue = now;
            if (baseEntity.UpdatedAtUtc == default)
                entry.Property(nameof(BaseEntity.UpdatedAtUtc)).CurrentValue = now;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Property(nameof(BaseEntity.UpdatedAtUtc)).CurrentValue = now;
        }
    }

    private static AuditLog? BuildAuditLog(EntityEntry entry, Guid? actorId, DateTime now)
    {
        var entityType = entry.Metadata.ClrType.Name;

        var snapshots = entry.Properties
            .Select(p => new AuditPropertySnapshot(
                p.Metadata.Name,
                entry.State == EntityState.Added ? null : p.OriginalValue,
                entry.State == EntityState.Deleted ? null : p.CurrentValue,
                p.IsModified))
            .ToList();

        var (action, diff) = entry.State switch
        {
            EntityState.Added => (AuditAction.Created, AuditDiffBuilder.BuildForCreate(entityType, snapshots)),
            EntityState.Deleted => (AuditAction.Deleted, AuditDiffBuilder.BuildForDelete(entityType, snapshots)),
            _ => BuildForModified(entry, entityType, snapshots)
        };

        if (diff is null)
            return null;

        return new AuditLog
        {
            Id = Guid.CreateVersion7(),
            EntityType = entityType,
            EntityId = BuildEntityId(entry),
            Action = action,
            ActorUserId = actorId,
            OldValuesJson = diff.OldValuesJson,
            NewValuesJson = diff.NewValuesJson,
            OccurredAtUtc = now
        };
    }

    private static (AuditAction Action, AuditDiff? Diff) BuildForModified(
        EntityEntry entry, string entityType, IReadOnlyList<AuditPropertySnapshot> snapshots)
    {
        if (entry.Entity is ISoftDeletable)
        {
            var deletedAt = entry.Property(nameof(ISoftDeletable.DeletedAtUtc));
            if (deletedAt.IsModified && deletedAt.OriginalValue is null && deletedAt.CurrentValue is not null)
                return (AuditAction.Deleted, AuditDiffBuilder.BuildForSoftDelete(entityType, snapshots));
            if (deletedAt.IsModified && deletedAt.OriginalValue is not null && deletedAt.CurrentValue is null)
                return (AuditAction.Restored, AuditDiffBuilder.BuildForRestore(entityType, snapshots));
        }

        return (AuditAction.Updated, AuditDiffBuilder.BuildForUpdate(entityType, snapshots));
    }

    // Single keys → "value"; composite keys (TimeEntryTag) → "value1:value2".
    private static string BuildEntityId(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()!.Properties;
        return string.Join(":", keyProperties.Select(p =>
            (entry.State == EntityState.Deleted
                ? entry.Property(p.Name).OriginalValue
                : entry.Property(p.Name).CurrentValue)?.ToString() ?? "?"));
    }
}

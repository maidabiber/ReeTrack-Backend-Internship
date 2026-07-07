using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Infrastructure.Auditing;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        // No HasDefaultValueSql: the interceptor always assigns the Id client-side.
        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(a => a.ActorUserId)
            .HasColumnName("actor_user_id");

        builder.Property(a => a.OldValuesJson)
            .HasColumnName("old_values")
            .HasColumnType("jsonb");

        builder.Property(a => a.NewValuesJson)
            .HasColumnName("new_values")
            .HasColumnType("jsonb");

        builder.Property(a => a.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .IsRequired();

        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("ix_audit_logs_entity");

        builder.HasIndex(a => a.ActorUserId)
            .HasDatabaseName("ix_audit_logs_actor_user_id");

        builder.HasIndex(a => a.OccurredAtUtc)
            .HasDatabaseName("ix_audit_logs_occurred_at_utc");
    }
}

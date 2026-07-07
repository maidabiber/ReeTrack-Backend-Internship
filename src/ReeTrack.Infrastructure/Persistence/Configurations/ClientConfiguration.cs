using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(c => c.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(c => c.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.Property(c => c.DeletedByUserId)
            .HasColumnName("deleted_by_user_id");

        builder.HasQueryFilter(c => c.DeletedAtUtc == null);

        // Filtered so a soft-deleted client does not block reusing its name.
        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName("ix_clients_name")
            .HasFilter("deleted_at_utc IS NULL");
    }
}

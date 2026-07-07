using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Color)
            .HasColumnName("color")
            .HasMaxLength(7);

        builder.Property(t => t.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(t => t.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.Property(t => t.DeletedByUserId)
            .HasColumnName("deleted_by_user_id");

        builder.HasQueryFilter(t => t.DeletedAtUtc == null);

        // Filtered so a soft-deleted tag does not block reusing its name.
        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasDatabaseName("ix_tags_name")
            .HasFilter("deleted_at_utc IS NULL");
    }
}

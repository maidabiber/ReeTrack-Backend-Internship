using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class CustomReportDefinitionConfiguration : IEntityTypeConfiguration<CustomReportDefinition>
{
    public void Configure(EntityTypeBuilder<CustomReportDefinition> builder)
    {
        builder.ToTable("custom_report_definitions");

        builder.HasKey(definition => definition.Id);

        builder.Property(definition => definition.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(definition => definition.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(definition => definition.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(definition => definition.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(definition => definition.SpecJson)
            .HasColumnName("spec_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(definition => definition.SchemaVersion)
            .HasColumnName("schema_version")
            .IsRequired();

        builder.Property(definition => definition.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(definition => definition.Visibility)
            .HasColumnName("visibility")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(definition => definition.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(definition => definition.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(definition => definition.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.Property(definition => definition.DeletedByUserId)
            .HasColumnName("deleted_by_user_id");

        builder.HasQueryFilter(definition => definition.DeletedAtUtc == null);

        // Scoped to the creator, not global — two admins can each save a report named "Q3 Margin".
        builder.HasIndex(definition => new { definition.CreatedByUserId, definition.NormalizedName })
            .IsUnique()
            .HasDatabaseName("ux_custom_report_definitions_owner_normalized_name")
            .HasFilter("deleted_at_utc IS NULL");

        builder.HasOne(definition => definition.CreatedByUser)
            .WithMany()
            .HasForeignKey(definition => definition.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

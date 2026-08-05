using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class ProjectThresholdConfiguration : IEntityTypeConfiguration<ProjectThreshold>
{
    public void Configure(EntityTypeBuilder<ProjectThreshold> builder)
    {
        builder.ToTable("project_thresholds");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(t => t.MetricType)
            .HasColumnName("metric_type")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(t => t.ThresholdPercentage)
            .HasColumnName("threshold_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(t => t.IsTriggered)
            .HasColumnName("is_triggered")
            .IsRequired();

        builder.Property(t => t.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(t => new { t.ProjectId, t.MetricType, t.ThresholdPercentage })
            .IsUnique()
            .HasDatabaseName("ix_project_thresholds_project_id_metric_type_threshold_percentage");

        builder.HasOne(t => t.Project)
            .WithMany(p => p.Thresholds)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

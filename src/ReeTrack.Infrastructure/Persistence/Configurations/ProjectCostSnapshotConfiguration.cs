using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class ProjectCostSnapshotConfiguration : IEntityTypeConfiguration<ProjectCostSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectCostSnapshot> builder)
    {
        builder.ToTable("project_cost_snapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(s => s.CalculatedCost)
            .HasColumnName("calculated_cost")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.CalculatedAtUtc)
            .HasColumnName("calculated_at_utc")
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(s => s.ProjectId)
            .HasDatabaseName("ix_project_cost_snapshots_project_id");

        builder.HasOne(s => s.Project)
            .WithMany(p => p.CostSnapshots)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

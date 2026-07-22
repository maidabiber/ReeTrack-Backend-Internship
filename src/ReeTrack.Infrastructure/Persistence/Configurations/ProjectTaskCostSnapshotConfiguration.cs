using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class ProjectTaskCostSnapshotConfiguration : IEntityTypeConfiguration<ProjectTaskCostSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectTaskCostSnapshot> builder)
    {
        builder.ToTable("project_task_cost_snapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.ProjectCostSnapshotId)
            .HasColumnName("project_cost_snapshot_id")
            .IsRequired();

        builder.Property(s => s.ProjectTaskId)
            .HasColumnName("project_task_id")
            .IsRequired();

        builder.Property(s => s.CalculatedCost)
            .HasColumnName("calculated_cost")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalHours)
            .HasColumnName("total_hours")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.WeekendHours)
            .HasColumnName("weekend_hours")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.HolidayHours)
            .HasColumnName("holiday_hours")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.OvertimeHours)
            .HasColumnName("overtime_hours")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(s => s.ProjectCostSnapshotId)
            .HasDatabaseName("ix_project_task_cost_snapshots_project_cost_snapshot_id");

        builder.HasIndex(s => s.ProjectTaskId)
            .HasDatabaseName("ix_project_task_cost_snapshots_project_task_id");

        builder.HasOne(s => s.ProjectCostSnapshot)
            .WithMany(s => s.TaskCosts)
            .HasForeignKey(s => s.ProjectCostSnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ProjectTask)
            .WithMany()
            .HasForeignKey(s => s.ProjectTaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

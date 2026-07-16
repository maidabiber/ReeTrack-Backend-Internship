using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class TimeEntryTemplateConfiguration : IEntityTypeConfiguration<TimeEntryTemplate>
{
    public void Configure(EntityTypeBuilder<TimeEntryTemplate> builder)
    {
        builder.ToTable("time_entry_templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(t => t.TimeEntryId)
            .HasColumnName("time_entry_id")
            .IsRequired();

        builder.Property(t => t.ProjectId)
            .HasColumnName("project_id");

        builder.Property(t => t.ProjectTaskId)
            .HasColumnName("project_task_id");

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(t => t.IsBillable)
            .HasColumnName("is_billable")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(t => t.StartTimeUtc)
            .HasColumnName("start_time_utc");

        builder.Property(t => t.EndTimeUtc)
            .HasColumnName("end_time_utc");

        builder.Property(t => t.DurationSeconds)
            .HasColumnName("duration_seconds")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(t => t.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("ix_time_entry_templates_user_id");

        builder.HasIndex(t => t.TimeEntryId)
            .IsUnique()
            .HasDatabaseName("ix_time_entry_templates_time_entry_id");

        builder.HasOne(t => t.User)
            .WithMany(u => u.TimeEntryTemplates)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.TimeEntry)
            .WithMany()
            .HasForeignKey(t => t.TimeEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Project)
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.ProjectTask)
            .WithMany()
            .HasForeignKey(t => t.ProjectTaskId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

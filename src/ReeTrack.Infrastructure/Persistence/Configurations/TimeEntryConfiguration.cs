using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("time_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(e => e.ClientId)
            .HasColumnName("client_id");

        builder.Property(e => e.ProjectId)
            .HasColumnName("project_id");

        builder.Property(e => e.ProjectTaskId)
            .HasColumnName("project_task_id");

        builder.Property(e => e.IsBillable)
            .HasColumnName("is_billable")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(e => e.Mode)
            .HasColumnName("mode")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(e => e.StartedAtUtc)
            .HasColumnName("started_at_utc");

        builder.Property(e => e.EndedAtUtc)
            .HasColumnName("ended_at_utc");

        builder.Property(e => e.DurationSeconds)
            .HasColumnName("duration_seconds")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_time_entries_user_id");

        builder.HasIndex(e => e.ProjectId)
            .HasDatabaseName("ix_time_entries_project_id");

        builder.HasIndex(e => e.StartedAtUtc)
            .HasDatabaseName("ix_time_entries_started_at_utc");

        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasDatabaseName("ix_time_entries_user_running")
            .HasFilter($"mode = {(short)TimeEntryMode.Timer} AND ended_at_utc IS NULL");

        builder.HasOne(e => e.User)
            .WithMany(u => u.TimeEntries)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ProjectTask)
            .WithMany()
            .HasForeignKey(e => e.ProjectTaskId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

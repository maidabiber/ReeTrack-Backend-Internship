using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class TimesheetConfiguration : IEntityTypeConfiguration<Timesheet>
{
    public void Configure(EntityTypeBuilder<Timesheet> builder)
    {
        builder.ToTable("timesheets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(t => t.WeekStartDate)
            .HasColumnName("week_start_date")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<short>()
            .HasDefaultValue(TimesheetStatus.Submitted)
            .IsRequired();

        builder.Property(t => t.SubmittedAtUtc)
            .HasColumnName("submitted_at_utc")
            .IsRequired();

        builder.Property(t => t.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id");

        builder.Property(t => t.ReviewedAtUtc)
            .HasColumnName("reviewed_at_utc");

        builder.Property(t => t.ReviewComment)
            .HasColumnName("review_comment")
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(t => new { t.UserId, t.WeekStartDate })
            .IsUnique()
            .HasDatabaseName("ix_timesheets_user_week");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_timesheets_status");

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ReviewedByUser)
            .WithMany()
            .HasForeignKey(t => t.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

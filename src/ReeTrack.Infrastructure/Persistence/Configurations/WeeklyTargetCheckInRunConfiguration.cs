using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class WeeklyTargetCheckInRunConfiguration : IEntityTypeConfiguration<WeeklyTargetCheckInRun>
{
    public void Configure(EntityTypeBuilder<WeeklyTargetCheckInRun> builder)
    {
        builder.ToTable("weekly_target_check_in_runs");

        builder.HasKey(run => run.Id);

        builder.Property(run => run.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(run => run.WeekStartDate)
            .HasColumnName("week_start_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(run => run.RanAtUtc)
            .HasColumnName("ran_at_utc")
            .IsRequired();

        builder.Property(run => run.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(run => run.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(run => run.WeekStartDate)
            .IsUnique()
            .HasDatabaseName("ux_weekly_target_check_in_runs_week_start_date");
    }
}

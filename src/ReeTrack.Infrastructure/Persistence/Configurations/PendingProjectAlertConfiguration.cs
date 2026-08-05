using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class PendingProjectAlertConfiguration : IEntityTypeConfiguration<PendingProjectAlert>
{
    public void Configure(EntityTypeBuilder<PendingProjectAlert> builder)
    {
        builder.ToTable("pending_project_alerts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(a => a.ThresholdId)
            .HasColumnName("threshold_id")
            .IsRequired();

        builder.Property(a => a.MetricType)
            .HasColumnName("metric_type")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(a => a.ProjectName)
            .HasColumnName("project_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.ThresholdPercentage)
            .HasColumnName("threshold_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.CostPercentage)
            .HasColumnName("cost_percentage")
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(a => a.CalculatedCost)
            .HasColumnName("calculated_cost")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.FixedFeeAmount)
            .HasColumnName("fixed_fee_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(a => a.HoursPercentage)
            .HasColumnName("hours_percentage")
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(a => a.ActualHours)
            .HasColumnName("actual_hours")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(a => a.TimeEstimateHours)
            .HasColumnName("time_estimate_hours")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(a => a.DeliverAfterUtc)
            .HasColumnName("deliver_after_utc")
            .IsRequired();

        builder.Property(a => a.DeliveredAtUtc)
            .HasColumnName("delivered_at_utc");

        builder.Property(a => a.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(a => a.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(a => new { a.DeliveredAtUtc, a.DeliverAfterUtc })
            .HasDatabaseName("ix_pending_project_alerts_delivered_deliver_after");

        builder.HasOne(a => a.Project)
            .WithMany()
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Threshold)
            .WithMany()
            .HasForeignKey(a => a.ThresholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

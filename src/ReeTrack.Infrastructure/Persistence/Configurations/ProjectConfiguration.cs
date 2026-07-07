using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(p => p.BillingType)
            .HasColumnName("billing_type")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(p => p.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .HasDefaultValue("EUR")
            .IsRequired();

        builder.Property(p => p.BudgetAmount)
            .HasColumnName("budget_amount")
            .HasPrecision(18, 2);

        builder.Property(p => p.FixedFeeAmount)
            .HasColumnName("fixed_fee_amount")
            .HasPrecision(18, 2);

        builder.Property(p => p.HourlyRate)
            .HasColumnName("hourly_rate")
            .HasPrecision(18, 2);

        builder.Property(p => p.TimeEstimateHours)
            .HasColumnName("time_estimate_hours")
            .HasPrecision(10, 2);

        builder.Property(p => p.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(p => p.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.Property(p => p.DeletedByUserId)
            .HasColumnName("deleted_by_user_id");

        builder.HasQueryFilter(p => p.DeletedAtUtc == null);

        builder.HasIndex(p => p.ClientId)
            .HasDatabaseName("ix_projects_client_id");

        builder.HasOne(p => p.Client)
            .WithMany(c => c.Projects)
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class UserHourlyRateConfiguration : IEntityTypeConfiguration<UserHourlyRate>
{
    public void Configure(EntityTypeBuilder<UserHourlyRate> builder)
    {
        builder.ToTable("user_hourly_rates");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.OwnsOne(r => r.Rate, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("hourly_rate")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(m => m.CurrencyCode)
                .HasColumnName("currency_code")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Navigation(r => r.Rate).IsRequired();

        builder.Property(r => r.ValidFrom)
            .HasColumnName("valid_from")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(r => r.ValidTo)
            .HasColumnName("valid_to")
            .HasColumnType("date");

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(r => r.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(r => new { r.UserId, r.ValidFrom })
            .IsUnique()
            .HasDatabaseName("ix_user_hourly_rates_user_id_valid_from");

        builder.HasOne(r => r.User)
            .WithMany(u => u.HourlyRates)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

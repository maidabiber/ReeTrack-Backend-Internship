using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class RateMultiplierSettingsConfiguration : IEntityTypeConfiguration<RateMultiplierSettings>
{
    public static readonly Guid DefaultSettingsId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    public void Configure(EntityTypeBuilder<RateMultiplierSettings> builder)
    {
        builder.ToTable("rate_multiplier_settings");

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .HasColumnName("id");

        builder.Property(settings => settings.WeekendPremium)
            .HasColumnName("weekend_premium")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(settings => settings.HolidayPremium)
            .HasColumnName("holiday_premium")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(settings => settings.OvertimePremium)
            .HasColumnName("overtime_premium")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(settings => settings.WeeklyOvertimeThresholdHours)
            .HasColumnName("weekly_overtime_threshold_hours")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(settings => settings.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(settings => settings.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(new RateMultiplierSettings
        {
            Id = DefaultSettingsId,
            WeekendPremium = 0.5m,
            HolidayPremium = 1.0m,
            OvertimePremium = 0.5m,
            WeeklyOvertimeThresholdHours = 40m,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt
        });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Constants;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class HourTargetSettingsConfiguration : IEntityTypeConfiguration<HourTargetSettings>
{
    public void Configure(EntityTypeBuilder<HourTargetSettings> builder)
    {
        builder.ToTable("hour_target_settings");

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .HasColumnName("id");

        builder.Property(settings => settings.Mode)
            .HasColumnName("mode")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(settings => settings.TargetHours)
            .HasColumnName("target_hours")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(settings => settings.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(settings => settings.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var seed = HourTargetSettings.CreateDefault(HourTargetDefaults.SettingsId, seededAt);

        builder.HasData(seed);
    }
}

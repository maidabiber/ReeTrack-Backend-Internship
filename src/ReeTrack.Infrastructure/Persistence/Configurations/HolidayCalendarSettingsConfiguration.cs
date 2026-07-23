using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class HolidayCalendarSettingsConfiguration : IEntityTypeConfiguration<HolidayCalendarSettings>
{
    public static readonly Guid DefaultSettingsId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    public void Configure(EntityTypeBuilder<HolidayCalendarSettings> builder)
    {
        builder.ToTable("holiday_calendar_settings");

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .HasColumnName("id");

        builder.Property(settings => settings.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2);

        builder.Property(settings => settings.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(settings => settings.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(new HolidayCalendarSettings
        {
            Id = DefaultSettingsId,
            CountryCode = null,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt
        });
    }
}

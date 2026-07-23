using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("holidays");

        builder.HasKey(holiday => holiday.Id);

        builder.Property(holiday => holiday.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(holiday => holiday.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(holiday => holiday.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(holiday => holiday.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(holiday => holiday.Source)
            .HasColumnName("source")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(holiday => holiday.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2);

        builder.Property(holiday => holiday.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(holiday => holiday.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(holiday => holiday.Date)
            .IsUnique()
            .HasDatabaseName("ux_holidays_date");
    }
}

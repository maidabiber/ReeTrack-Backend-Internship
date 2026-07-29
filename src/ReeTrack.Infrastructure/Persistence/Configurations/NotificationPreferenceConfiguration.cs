using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(p => p.NotificationType)
            .HasColumnName("notification_type")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(p => p.DeliveryChannel)
            .HasColumnName("delivery_channel")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(p => p.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(p => new { p.UserId, p.NotificationType, p.DeliveryChannel })
            .IsUnique()
            .HasDatabaseName("ix_notification_preferences_user_type_channel");

        builder.HasOne(p => p.User)
            .WithMany(u => u.NotificationPreferences)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

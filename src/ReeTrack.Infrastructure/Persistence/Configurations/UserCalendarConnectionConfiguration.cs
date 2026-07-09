using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class UserCalendarConnectionConfiguration : IEntityTypeConfiguration<UserCalendarConnection>
{
    public void Configure(EntityTypeBuilder<UserCalendarConnection> builder)
    {
        builder.ToTable("user_calendar_connections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(c => c.ProviderType)
            .HasColumnName("provider_type")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(c => c.AccessToken)
            .HasColumnName("access_token")
            .IsRequired();

        builder.Property(c => c.RefreshToken)
            .HasColumnName("refresh_token")
            .IsRequired();

        builder.Property(c => c.ExpirationDateTime)
            .HasColumnName("expiration_date_time")
            .IsRequired();

        builder.Property(c => c.ProviderAccountId)
            .HasColumnName("provider_account_id")
            .HasMaxLength(320);

        builder.Property(c => c.LastSyncedAtUtc)
            .HasColumnName("last_synced_at_utc");

        builder.Property(c => c.SyncStatus)
            .HasColumnName("sync_status")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(c => c.LastSyncError)
            .HasColumnName("last_sync_error")
            .HasMaxLength(2000);

        builder.Property(c => c.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(c => c.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(c => new { c.UserId, c.ProviderType })
            .IsUnique()
            .HasDatabaseName("ix_user_calendar_connections_user_id_provider_type");

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

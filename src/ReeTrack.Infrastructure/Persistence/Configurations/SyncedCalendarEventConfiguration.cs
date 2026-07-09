using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class SyncedCalendarEventConfiguration : IEntityTypeConfiguration<SyncedCalendarEvent>
{
    public void Configure(EntityTypeBuilder<SyncedCalendarEvent> builder)
    {
        builder.ToTable("synced_calendar_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ConnectionId)
            .HasColumnName("connection_id")
            .IsRequired();

        builder.Property(e => e.ExternalEventId)
            .HasColumnName("external_event_id")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.StartAtUtc)
            .HasColumnName("start_at_utc")
            .IsRequired();

        builder.Property(e => e.EndAtUtc)
            .HasColumnName("end_at_utc")
            .IsRequired();

        builder.Property(e => e.IsAllDay)
            .HasColumnName("is_all_day")
            .IsRequired();

        builder.Property(e => e.Location)
            .HasColumnName("location")
            .HasMaxLength(500);

        builder.Property(e => e.HtmlLink)
            .HasColumnName("html_link");

        builder.Property(e => e.RawUpdatedAtUtc)
            .HasColumnName("raw_updated_at_utc");

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(e => new { e.ConnectionId, e.ExternalEventId })
            .IsUnique()
            .HasDatabaseName("ix_synced_calendar_events_connection_id_external_event_id");

        builder.HasOne(e => e.Connection)
            .WithMany(c => c.SyncedEvents)
            .HasForeignKey(e => e.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

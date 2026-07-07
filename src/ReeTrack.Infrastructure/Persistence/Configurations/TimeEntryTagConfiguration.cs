using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class TimeEntryTagConfiguration : IEntityTypeConfiguration<TimeEntryTag>
{
    public void Configure(EntityTypeBuilder<TimeEntryTag> builder)
    {
        builder.ToTable("time_entry_tags");

        builder.HasKey(tet => new { tet.TimeEntryId, tet.TagId });

        builder.Property(tet => tet.TimeEntryId)
            .HasColumnName("time_entry_id");

        builder.Property(tet => tet.TagId)
            .HasColumnName("tag_id");

        // Must mirror the principals' soft-delete filters, or EF warns and joins
        // through this table would surface rows pointing at hidden entities.
        builder.HasQueryFilter(tet =>
            tet.TimeEntry.DeletedAtUtc == null && tet.Tag.DeletedAtUtc == null);

        builder.HasOne(tet => tet.TimeEntry)
            .WithMany(te => te.TimeEntryTags)
            .HasForeignKey(tet => tet.TimeEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tet => tet.Tag)
            .WithMany(t => t.TimeEntryTags)
            .HasForeignKey(tet => tet.TagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class TimeEntryTemplateTagConfiguration : IEntityTypeConfiguration<TimeEntryTemplateTag>
{
    public void Configure(EntityTypeBuilder<TimeEntryTemplateTag> builder)
    {
        builder.ToTable("time_entry_template_tags");

        builder.HasKey(tet => new { tet.TimeEntryTemplateId, tet.TagId });

        builder.Property(tet => tet.TimeEntryTemplateId)
            .HasColumnName("time_entry_template_id");

        builder.Property(tet => tet.TagId)
            .HasColumnName("tag_id");

        // Templates are hard-deleted; only Tag soft-delete needs mirroring so joins
        // do not surface rows pointing at hidden tags.
        builder.HasQueryFilter(tet => tet.Tag.DeletedAtUtc == null);

        builder.HasOne(tet => tet.TimeEntryTemplate)
            .WithMany(t => t.TimeEntryTemplateTags)
            .HasForeignKey(tet => tet.TimeEntryTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tet => tet.Tag)
            .WithMany(t => t.TimeEntryTemplateTags)
            .HasForeignKey(tet => tet.TagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

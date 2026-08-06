using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class ReportShareRecipientConfiguration : IEntityTypeConfiguration<ReportShareRecipient>
{
    public void Configure(EntityTypeBuilder<ReportShareRecipient> builder)
    {
        builder.ToTable("report_share_recipients");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.ShareLinkId)
            .HasColumnName("share_link_id")
            .IsRequired();

        builder.Property(r => r.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasIndex(r => new { r.ShareLinkId, r.UserId })
            .IsUnique()
            .HasDatabaseName("ux_report_share_recipients_link_user");

        builder.HasOne(r => r.ShareLink)
            .WithMany(link => link.Recipients)
            .HasForeignKey(r => r.ShareLinkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(r => r.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();
    }
}

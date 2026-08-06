using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class ReportShareLinkConfiguration : IEntityTypeConfiguration<ReportShareLink>
{
    public void Configure(EntityTypeBuilder<ReportShareLink> builder)
    {
        builder.ToTable("report_share_links");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(link => link.Token)
            .HasColumnName("token")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(link => link.ReportType)
            .HasColumnName("report_type")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(link => link.ReportId)
            .HasColumnName("report_id");

        builder.Property(link => link.QueryJson)
            .HasColumnName("query_json")
            .HasMaxLength(8000);

        builder.Property(link => link.SpecJson)
            .HasColumnName("spec_json");

        builder.Property(link => link.AccessLevel)
            .HasColumnName("access_level")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(link => link.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(link => link.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(link => link.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(link => link.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(link => link.Token)
            .IsUnique()
            .HasDatabaseName("ux_report_share_links_token");

        builder.HasIndex(link => link.ReportType)
            .HasDatabaseName("ix_report_share_links_report_type");

        builder.HasOne(link => link.CreatedByUser)
            .WithMany()
            .HasForeignKey(link => link.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

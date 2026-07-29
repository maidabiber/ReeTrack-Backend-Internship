using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class ReportFilterSetConfiguration : IEntityTypeConfiguration<ReportFilterSet>
{
    public void Configure(EntityTypeBuilder<ReportFilterSet> builder)
    {
        builder.ToTable("report_filter_sets");

        builder.HasKey(filterSet => filterSet.Id);

        builder.Property(filterSet => filterSet.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(filterSet => filterSet.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(filterSet => filterSet.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(filterSet => filterSet.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(filterSet => filterSet.QueryJson)
            .HasColumnName("query_json")
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(filterSet => filterSet.SchemaVersion)
            .HasColumnName("schema_version")
            .IsRequired();

        builder.Property(filterSet => filterSet.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(filterSet => filterSet.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(filterSet => new { filterSet.UserId, filterSet.NormalizedName })
            .IsUnique()
            .HasDatabaseName("ux_report_filter_sets_user_id_normalized_name");

        builder.HasOne(filterSet => filterSet.User)
            .WithMany()
            .HasForeignKey(filterSet => filterSet.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

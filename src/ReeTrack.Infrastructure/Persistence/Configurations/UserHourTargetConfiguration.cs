using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class UserHourTargetConfiguration : IEntityTypeConfiguration<UserHourTarget>
{
    public void Configure(EntityTypeBuilder<UserHourTarget> builder)
    {
        builder.ToTable("user_hour_targets");

        builder.HasKey(target => target.Id);

        builder.Property(target => target.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(target => target.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(target => target.Mode)
            .HasColumnName("mode")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(target => target.TargetHours)
            .HasColumnName("target_hours")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(target => target.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(target => target.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(target => target.UserId)
            .IsUnique()
            .HasDatabaseName("ix_user_hour_targets_user_id");

        builder.HasOne(target => target.User)
            .WithMany(user => user.HourTargets)
            .HasForeignKey(target => target.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

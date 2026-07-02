using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.UserId)
            .HasColumnName("user_id");

        builder.Property(ur => ur.RoleId)
            .HasColumnName("role_id");

        builder.Property(ur => ur.AssignedAtUtc)
            .HasColumnName("assigned_at_utc")
            .IsRequired();

        builder.Property(ur => ur.AssignedByUserId)
            .HasColumnName("assigned_by_user_id");

        builder.HasIndex(ur => ur.RoleId)
            .HasDatabaseName("ix_user_roles_role_id");

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ur => ur.AssignedByUser)
            .WithMany(u => u.AssignedRoles)
            .HasForeignKey(ur => ur.AssignedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

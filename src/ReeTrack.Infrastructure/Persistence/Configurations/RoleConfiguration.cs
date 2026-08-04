using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    private static readonly DateTime SeedTimestamp = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(200);

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(r => r.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("ix_roles_name");

        builder.HasData(
            new Role
            {
                Id = 1,
                Name = "Admin",
                Description = "Full platform access",
                CreatedAtUtc = SeedTimestamp,
                UpdatedAtUtc = SeedTimestamp
            },
            new Role
            {
                Id = 2,
                Name = "Member",
                Description = "Standard user access",
                CreatedAtUtc = SeedTimestamp,
                UpdatedAtUtc = SeedTimestamp
            },
            new Role
            {
                Id = 3,
                Name = "ProjectManager",
                Description = "Project and team oversight access",
                CreatedAtUtc = SeedTimestamp,
                UpdatedAtUtc = SeedTimestamp
            });
    }
}

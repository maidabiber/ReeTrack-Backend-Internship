using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(i => i.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(i => i.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(i => i.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.Property(i => i.InvitedByUserId)
            .HasColumnName("invited_by_user_id")
            .IsRequired();

        builder.Property(i => i.AcceptedAtUtc)
            .HasColumnName("accepted_at_utc");

        builder.Property(i => i.AcceptedByUserId)
            .HasColumnName("accepted_by_user_id");

        builder.Property(i => i.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(i => i.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(i => i.TokenHash)
            .HasDatabaseName("ix_invitations_token_hash");

        builder.HasIndex(i => i.Email)
            .IsUnique()
            .HasDatabaseName("ix_invitations_email_pending")
            .HasFilter($"status = {(short)InvitationStatus.Pending}");

        builder.HasOne(i => i.Role)
            .WithMany(r => r.Invitations)
            .HasForeignKey(i => i.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.InvitedByUser)
            .WithMany(u => u.SentInvitations)
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.AcceptedByUser)
            .WithMany(u => u.AcceptedInvitations)
            .HasForeignKey(i => i.AcceptedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

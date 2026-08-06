using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class JiraWebhookSettingsConfiguration : IEntityTypeConfiguration<JiraWebhookSettings>
{
    public void Configure(EntityTypeBuilder<JiraWebhookSettings> builder)
    {
        builder.ToTable("jira_webhook_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SingletonKey).HasColumnName("singleton_key").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.LastReceivedAtUtc).HasColumnName("last_received_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasIndex(x => x.SingletonKey)
            .IsUnique()
            .HasDatabaseName("ix_jira_webhook_settings_singleton_key");
    }
}

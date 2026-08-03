using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(invoice => invoice.Number)
            .HasColumnName("number")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(invoice => invoice.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(invoice => invoice.ClientName)
            .HasColumnName("client_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(invoice => invoice.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasDefaultValue("EUR")
            .IsRequired();

        builder.Property(invoice => invoice.PeriodFrom)
            .HasColumnName("period_from")
            .IsRequired();

        builder.Property(invoice => invoice.PeriodTo)
            .HasColumnName("period_to")
            .IsRequired();

        builder.Property(invoice => invoice.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(invoice => invoice.Status)
            .HasColumnName("status")
            .HasConversion<short>()
            .HasDefaultValue(InvoiceStatus.Draft)
            .IsRequired();

        builder.Property(invoice => invoice.GeneratedByUserId)
            .HasColumnName("generated_by_user_id")
            .IsRequired();

        builder.Property(invoice => invoice.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(invoice => invoice.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(invoice => invoice.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.Property(invoice => invoice.DeletedByUserId)
            .HasColumnName("deleted_by_user_id");

        builder.HasQueryFilter(invoice => invoice.DeletedAtUtc == null);

        builder.HasIndex(invoice => invoice.Number)
            .IsUnique()
            .HasDatabaseName("ux_invoices_number")
            .HasFilter("deleted_at_utc IS NULL");

        builder.HasIndex(invoice => invoice.ClientId)
            .HasDatabaseName("ix_invoices_client_id");

        builder.HasIndex(invoice => invoice.Status)
            .HasDatabaseName("ix_invoices_status");

        builder.HasIndex(invoice => new { invoice.PeriodFrom, invoice.PeriodTo })
            .HasDatabaseName("ix_invoices_period");

        builder.HasOne(invoice => invoice.Client)
            .WithMany(client => client.Invoices)
            .HasForeignKey(invoice => invoice.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invoice => invoice.GeneratedByUser)
            .WithMany()
            .HasForeignKey(invoice => invoice.GeneratedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

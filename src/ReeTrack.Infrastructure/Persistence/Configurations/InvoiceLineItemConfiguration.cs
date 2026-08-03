using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Persistence.Configurations;

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("invoice_line_items");

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(line => line.InvoiceId)
            .HasColumnName("invoice_id")
            .IsRequired();

        builder.Property(line => line.ProjectId)
            .HasColumnName("project_id");

        builder.Property(line => line.Description)
            .HasColumnName("description")
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(line => line.BillingModel)
            .HasColumnName("billing_model")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(line => line.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(line => line.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(line => line.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(line => line.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(line => line.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(line => line.InvoiceId)
            .HasDatabaseName("ix_invoice_line_items_invoice_id");

        builder.HasIndex(line => line.ProjectId)
            .HasDatabaseName("ix_invoice_line_items_project_id");

        builder.HasOne(line => line.Invoice)
            .WithMany(invoice => invoice.LineItems)
            .HasForeignKey(line => line.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(line => line.Project)
            .WithMany()
            .HasForeignKey(line => line.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

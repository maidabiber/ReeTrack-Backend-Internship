using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Invoices;
using Xunit;

namespace ReeTrack.UnitTests.Invoices;

public class PdfInvoiceWriterTests
{
    [Fact]
    public void Write_EmitsPdfMagicBytesAndFilenameFromInvoiceNumber()
    {
        var invoice = new InvoiceDto
        {
            Id = Guid.NewGuid(),
            Number = "INV-20260730-ABCD1234",
            ClientId = Guid.NewGuid(),
            ClientName = "Acme",
            CurrencyCode = "EUR",
            PeriodFrom = new DateOnly(2026, 7, 1),
            PeriodTo = new DateOnly(2026, 7, 31),
            Subtotal = 150m,
            Status = InvoiceStatus.Draft,
            GeneratedByUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            LineItems =
            [
                new InvoiceLineItemDto
                {
                    Id = Guid.NewGuid(),
                    ProjectId = Guid.NewGuid(),
                    Description = "Website · Hourly · 2h billable",
                    BillingModel = InvoiceLineBillingModel.Hourly,
                    Quantity = 2m,
                    UnitPrice = 75m,
                    Amount = 150m,
                    SortOrder = 0
                }
            ]
        };

        var file = new PdfInvoiceWriter().Write(invoice);

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("INV-20260730-ABCD1234.pdf", file.FileName);
        Assert.True(file.Bytes.Length > 100);
        Assert.Equal("%PDF"u8.ToArray(), file.Bytes.Take(4).ToArray());
    }

    [Fact]
    public void Write_WithNewerInvoiceNumber_DoesNotThrow()
    {
        var invoice = new InvoiceDto
        {
            Id = Guid.NewGuid(),
            Number = "INV-20260730-ABCD1234",
            ClientId = Guid.NewGuid(),
            ClientName = "Acme",
            CurrencyCode = "EUR",
            PeriodFrom = new DateOnly(2026, 7, 1),
            PeriodTo = new DateOnly(2026, 7, 31),
            Subtotal = 150m,
            Status = InvoiceStatus.Draft,
            GeneratedByUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            NewerInvoiceId = Guid.NewGuid(),
            NewerInvoiceNumber = "INV-20260731-EFGH5678",
            LineItems =
            [
                new InvoiceLineItemDto
                {
                    Id = Guid.NewGuid(),
                    ProjectId = Guid.NewGuid(),
                    Description = "Website · Hourly · 2h billable",
                    BillingModel = InvoiceLineBillingModel.Hourly,
                    Quantity = 2m,
                    UnitPrice = 75m,
                    Amount = 150m,
                    SortOrder = 0
                }
            ]
        };

        var file = new PdfInvoiceWriter().Write(invoice);

        Assert.NotNull(file);
        Assert.Equal("application/pdf", file.ContentType);
    }
}

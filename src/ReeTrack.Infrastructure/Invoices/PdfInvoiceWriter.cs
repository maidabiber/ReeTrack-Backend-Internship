using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Reports.Writers;

namespace ReeTrack.Infrastructure.Invoices;

/// <summary>RT-210 — QuestPDF export for a persisted client invoice draft.</summary>
public sealed class PdfInvoiceWriter
{
    public ReportFile Write(InvoiceDto invoice)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(ReportColors.Navy));
                page.PageColor(ReportColors.White);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("INVOICE")
                                .FontSize(9)
                                .FontColor(ReportColors.NavyMuted);
                            left.Item().PaddingTop(4).Text(invoice.Number)
                                .SemiBold()
                                .FontSize(18)
                                .FontColor(ReportColors.Navy);
                        });
                        row.ConstantItem(140).AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text(invoice.Status.ToString().ToUpperInvariant())
                                .FontSize(9)
                                .SemiBold()
                                .FontColor(ReportColors.NavyMuted);
                            right.Item().PaddingTop(4).AlignRight()
                                .Text(
                                    $"{ReportFormat.FriendlyDate(invoice.PeriodFrom)} – {ReportFormat.FriendlyDate(invoice.PeriodTo)}")
                                .FontSize(9)
                                .FontColor(ReportColors.NavyMuted);
                            right.Item().PaddingTop(4).AlignRight()
                                .Text($"Created: {ReportFormat.FriendlyDateTime(invoice.CreatedAtUtc)}")
                                .FontSize(8)
                                .FontColor(ReportColors.NavyMuted);
                        });
                    });

                    col.Item().PaddingTop(10).Height(2).Background(ReportColors.Brand);

                    if (!string.IsNullOrEmpty(invoice.NewerInvoiceNumber))
                    {
                        col.Item().PaddingTop(10).Background(ReportColors.BrandTint).Padding(8).Text(text =>
                        {
                            text.Span("Warning: A newer invoice exists for this client (").FontSize(9).FontColor(ReportColors.Navy);
                            text.Span(invoice.NewerInvoiceNumber).SemiBold().FontSize(9).FontColor(ReportColors.Navy);
                            text.Span(").").FontSize(9).FontColor(ReportColors.Navy);
                        });
                    }

                    col.Item().PaddingTop(14).Text("Bill to")
                        .FontSize(9)
                        .FontColor(ReportColors.NavyMuted);
                    col.Item().Text(invoice.ClientName).SemiBold().FontSize(12);
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3.2f);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(52);
                            columns.ConstantColumn(78);
                            columns.ConstantColumn(88);
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header, "Description");
                            HeaderCell(header, "Model");
                            HeaderCell(header, "Qty", alignRight: true);
                            HeaderCell(header, "Unit", alignRight: true);
                            HeaderCell(header, "Amount", alignRight: true);
                        });

                        var i = 0;
                        foreach (var line in invoice.LineItems
                                     .OrderBy(l => l.SortOrder)
                                     .ThenBy(l => l.Description, StringComparer.OrdinalIgnoreCase))
                        {
                            var zebra = i++ % 2 == 1;
                            BodyCell(table, line.Description, zebra);
                            BodyCell(table, BillingLabel(line.BillingModel), zebra);
                            BodyCell(table, FormatQty(line.Quantity), zebra, alignRight: true);
                            BodyCell(
                                table,
                                ReportFormat.Money(line.UnitPrice, invoice.CurrencyCode),
                                zebra,
                                alignRight: true);
                            BodyCell(
                                table,
                                ReportFormat.Money(line.Amount, invoice.CurrencyCode),
                                zebra,
                                alignRight: true);
                        }
                    });

                    col.Item().PaddingTop(16).AlignRight().Width(220).Background(ReportColors.SurfaceMuted)
                        .Padding(12)
                        .Row(row =>
                        {
                            row.RelativeItem().Text("Subtotal")
                                .FontSize(9)
                                .FontColor(ReportColors.NavyMuted);
                            row.ConstantItem(120).AlignRight().Text(
                                    ReportFormat.Money(invoice.Subtotal, invoice.CurrencyCode))
                                .SemiBold()
                                .FontSize(13);
                        });
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Page ").FontSize(8).FontColor(ReportColors.NavyMuted);
                    text.CurrentPageNumber().FontSize(8).FontColor(ReportColors.NavyMuted);
                    text.Span(" / ").FontSize(8).FontColor(ReportColors.NavyMuted);
                    text.TotalPages().FontSize(8).FontColor(ReportColors.NavyMuted);
                });
            });
        }).GeneratePdf();

        return new ReportFile(bytes, "application/pdf", $"{SanitizeFileName(invoice.Number)}.pdf");
    }

    private static string BillingLabel(InvoiceLineBillingModel model) =>
        model == InvoiceLineBillingModel.FixedFee ? "Fixed fee" : "Hourly";

    private static string FormatQty(decimal quantity) =>
        quantity == decimal.Truncate(quantity)
            ? quantity.ToString("0")
            : quantity.ToString("0.##");

    private static void HeaderCell(TableCellDescriptor header, string text, bool alignRight = false)
    {
        var cell = header.Cell().Element(c => c
            .Background(ReportColors.HeaderGrayBg)
            .PaddingVertical(6)
            .PaddingHorizontal(6));
        var content = alignRight ? cell.AlignRight() : cell;
        content.Text(text).SemiBold().FontSize(8).FontColor(ReportColors.HeaderGray);
    }

    private static void BodyCell(
        TableDescriptor table,
        string text,
        bool zebra,
        bool alignRight = false)
    {
        var cell = table.Cell().Element(c =>
        {
            if (zebra) c = c.Background(ReportColors.Canvas);
            return c.PaddingVertical(7).PaddingHorizontal(6);
        });
        var content = alignRight ? cell.AlignRight() : cell;
        content.Text(text).FontSize(9).FontColor(ReportColors.Navy);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (invalid.Contains(ch) || ch is '/' or '\\')
                sb.Append('-');
            else
                sb.Append(ch);
        }

        var sanitized = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "invoice" : sanitized;
    }
}

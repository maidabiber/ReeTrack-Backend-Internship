using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Infrastructure.Reports.Writers.Custom;

public sealed class CustomPdfReportWriter : IReportWriter<CustomReportDto>
{
    private const int KpisPerRow = 4;

    public ReportExportFormat Format => ReportExportFormat.Pdf;

    public ReportFile Write(CustomReportDto model)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(ReportColors.Navy));
                page.PageColor(ReportColors.White);

                page.Header().Column(col =>
                {
                    col.Item().Text("ReeTrack Custom Report").SemiBold().FontSize(14);
                    col.Item().Text(ReportFormat.PeriodLabel(model)).FontSize(9).FontColor(ReportColors.NavyMuted);
                    col.Item().PaddingTop(4).Text(
                            $"{ReportFormat.HoursLabel(model.Kpis.TotalSeconds)} · " +
                            $"{model.Kpis.EntryCount} entries · " +
                            $"{ReportFormat.Percent(model.Kpis.BillablePct)} billable")
                        .FontSize(8).FontColor(ReportColors.NavyMuted);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    foreach (var block in model.Blocks)
                        col.Item().PaddingBottom(14).Element(c => ComposeBlock(c, block));

                    col.Item().PaddingTop(6).Element(c =>
                        PdfBasisBlock.Compose(c, ReportFormat.CustomBasisLines(model.Basis)));
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ").FontSize(8).FontColor(ReportColors.NavyMuted);
                    text.CurrentPageNumber().FontSize(8).FontColor(ReportColors.NavyMuted);
                    text.Span(" / ").FontSize(8).FontColor(ReportColors.NavyMuted);
                    text.TotalPages().FontSize(8).FontColor(ReportColors.NavyMuted);
                });
            });
        }).GeneratePdf();

        return new ReportFile(
            bytes,
            "application/pdf",
            ReportFileNames.ForCustom(ReportExportFormat.Pdf, model.GeneratedAtUtc));
    }

    private static void ComposeBlock(IContainer container, ReportBlockResult block)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(block.Title))
                col.Item().Text(block.Title).SemiBold().FontSize(11);

            switch (block)
            {
                case KpiGroupResult kpi:
                    // A KPI block may hold up to 8 metrics; eight tiles across one A4 column
                    // squeezes every value to nothing. Wrap, padding the last row so the
                    // remaining tiles keep the same width as a full one.
                    foreach (var tiles in kpi.Cells.Chunk(KpisPerRow))
                    {
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            foreach (var cell in tiles)
                            {
                                row.RelativeItem().Border(1).BorderColor(ReportColors.Canvas).Padding(6).Column(tile =>
                                {
                                    tile.Item().Text(cell.Label).FontSize(7).FontColor(ReportColors.NavyMuted);
                                    tile.Item().PaddingTop(2).Text(cell.Display).SemiBold().FontSize(11);
                                });
                            }

                            for (var filler = tiles.Length; filler < KpisPerRow; filler++)
                                row.RelativeItem();
                        });
                    }
                    break;

                case TableResult table:
                    col.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in table.Columns)
                                columns.RelativeColumn();
                        });

                        t.Header(header =>
                        {
                            foreach (var column in table.Columns)
                                Header(header, column.Label);
                        });

                        var i = 0;
                        foreach (var dataRow in table.Rows)
                        {
                            // Group rows carry their own styling and don't participate in the
                            // zebra stripe — only detail rows advance the stripe counter.
                            var zebra = dataRow.Kind == TableRowKind.Detail && i++ % 2 == 1;
                            var columnIndex = 0;
                            foreach (var column in table.Columns)
                            {
                                var cell = dataRow.Cells.GetValueOrDefault(column.Key);
                                var indent = columnIndex == 0 ? dataRow.Depth : 0;
                                switch (dataRow.Kind)
                                {
                                    case TableRowKind.GroupHeader:
                                        Body(t, cell?.Display ?? "", zebra: false, boldOnly: true, indent: indent);
                                        break;
                                    case TableRowKind.GroupSubtotal:
                                        Body(t, cell?.Display ?? "", zebra: false, bold: true, indent: indent);
                                        break;
                                    default:
                                        Body(t, cell?.Display ?? "", zebra, indent: indent);
                                        break;
                                }
                                columnIndex++;
                            }
                        }

                        if (table.Totals is { } totals)
                        {
                            foreach (var column in table.Columns)
                            {
                                var cell = totals.Cells.GetValueOrDefault(column.Key);
                                Body(t, cell?.Display ?? "", zebra: false, bold: true);
                            }
                        }
                    });
                    break;

                case SeriesResult series:
                {
                    var svg = CustomChartSvg.Render(series);
                    if (svg is null)
                        col.Item().PaddingTop(6).Text("No series data.").FontColor(ReportColors.NavyMuted);
                    else
                        col.Item().PaddingTop(6).Height(150).Svg(svg);
                    break;
                }

                case ProseResult prose:
                    foreach (var paragraph in prose.Paragraphs)
                        col.Item().PaddingTop(4).Text(paragraph).FontSize(9);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(block.Footnote))
                col.Item().PaddingTop(4).Text(block.Footnote).FontSize(7).FontColor(ReportColors.NavyMuted);
        });
    }

    private static void Header(TableCellDescriptor header, string label, bool alignRight = false)
    {
        header.Cell().Element(c =>
        {
            var cell = c
                .DefaultTextStyle(x => x.SemiBold().FontSize(8))
                .Padding(3)
                .BorderBottom(1)
                .BorderColor(ReportColors.Canvas);
            if (alignRight)
                cell.AlignRight().Text(label);
            else
                cell.Text(label);
        });
    }

    private static void Body(
        TableDescriptor table,
        string value,
        bool zebra,
        bool bold = false,
        bool alignRight = false,
        bool boldOnly = false,
        int indent = 0)
    {
        table.Cell().Element(c =>
        {
            var cell = c.PaddingVertical(3).PaddingHorizontal(3 + indent * 8);
            if (zebra)
                cell = cell.Background(ReportColors.Canvas);
            if (bold)
                cell = cell.Background(ReportColors.BrandTint);
            if (alignRight)
                cell = cell.AlignRight();
            var text = cell.Text(value);
            if (bold || boldOnly)
                text.SemiBold();
        });
    }
}

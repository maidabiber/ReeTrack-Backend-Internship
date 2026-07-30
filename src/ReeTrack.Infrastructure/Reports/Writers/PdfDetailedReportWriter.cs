using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class PdfDetailedReportWriter : IDetailedReportWriter
{
    static PdfDetailedReportWriter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportExportFormat Format => ReportExportFormat.Pdf;

    public ReportFile Write(DetailedReportDto model)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(PageSizes.A4.Landscape());
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(ReportColors.Navy));
                page.PageColor(ReportColors.White);

                page.Header().Column(col =>
                {
                    col.Item().Text("ReeTrack Detailed Report").SemiBold().FontSize(14);
                    col.Item().Text(ReportFormat.PeriodLabel(model)).FontSize(9).FontColor(ReportColors.NavyMuted);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Text(
                        $"{ReportFormat.HoursLabel(model.Kpis.TotalSeconds)} · " +
                        $"{model.Kpis.EntryCount} entries · " +
                        $"{ReportFormat.Percent(model.Kpis.BillablePct)} billable");

                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(58);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(0.9f);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(48);
                            columns.ConstantColumn(52);
                        });

                        table.Header(header =>
                        {
                            Header(header, "Date");
                            Header(header, "Member");
                            Header(header, "Client");
                            Header(header, "Project");
                            Header(header, "Task");
                            Header(header, "Bill.");
                            Header(header, "Hours");
                            Header(header, "Cost");
                        });

                        var i = 0;
                        foreach (var (group, entries) in DetailedReportExportRows.Enumerate(model))
                        {
                            if (group is not null)
                                GroupHeader(table, DetailedReportExportRows.GroupSummary(group));

                            foreach (var entry in entries)
                            {
                                var zebra = i++ % 2 == 1;
                                Body(table, ReportFormat.IsoDate(entry.EntryDate), zebra);
                                Body(table, entry.DisplayName, zebra);
                                Body(table, string.IsNullOrWhiteSpace(entry.ClientName) ? "—" : entry.ClientName, zebra);
                                Body(table, entry.ProjectName ?? ReportFormat.UnassignedLabel, zebra);
                                Body(table, string.IsNullOrWhiteSpace(entry.TaskName) ? "—" : entry.TaskName, zebra);
                                Body(table, entry.IsBillable ? "Y" : "N", zebra);
                                Body(table, ReportFormat.HoursLabel(entry.DurationSeconds), zebra, alignRight: true);
                                Body(
                                    table,
                                    entry.CurrencyCode is { Length: > 0 } currency
                                        ? ReportFormat.Money(entry.CalculatedCost, currency)
                                        : entry.CalculatedCost.ToString("0.##"),
                                    zebra,
                                    alignRight: true);
                            }
                        }
                    });

                    col.Item().PaddingTop(10).Column(basis =>
                    {
                        foreach (var line in ReportFormat.BasisLines(model))
                            basis.Item().Text(line).FontSize(7).FontColor(ReportColors.NavyMuted);
                    });
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        return new ReportFile(
            bytes,
            "application/pdf",
            ReportFileNames.ForDetailed(ReportExportFormat.Pdf, model.GeneratedAtUtc));
    }

    private static void Header(TableCellDescriptor header, string text) =>
        header.Cell().Element(c => c
            .Background(ReportColors.HeaderGrayBg)
            .PaddingVertical(4).PaddingHorizontal(4)
            .Text(text).SemiBold().FontSize(7).FontColor(ReportColors.HeaderGray));

    private static void GroupHeader(TableDescriptor table, string text) =>
        table.Cell().ColumnSpan(8).Element(c => c
            .Background(ReportColors.BrandTint)
            .PaddingVertical(4).PaddingHorizontal(4)
            .Text(text).SemiBold().FontSize(8).FontColor(ReportColors.Navy));

    private static void Body(
        TableDescriptor table,
        string text,
        bool zebra = false,
        bool alignRight = false)
    {
        table.Cell().Element(c =>
        {
            var cell = c
                .Background(zebra ? ReportColors.SurfaceMuted : ReportColors.White)
                .BorderBottom(0.5f).BorderColor(ReportColors.Canvas)
                .PaddingVertical(3).PaddingHorizontal(4);
            if (alignRight)
                cell = cell.AlignRight();
            cell.Text(text).FontSize(7).FontColor(ReportColors.Navy);
        });
    }
}

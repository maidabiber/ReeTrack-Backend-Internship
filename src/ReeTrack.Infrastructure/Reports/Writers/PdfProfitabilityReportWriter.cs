using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class PdfProfitabilityReportWriter : IReportWriter<ProfitabilityReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Pdf;

    public ReportFile Write(ProfitabilityReportDto model)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(ReportColors.Navy));
                page.PageColor(ReportColors.White);

                page.Header().Column(col =>
                {
                    col.Item().Text("ReeTrack Profitability Report").SemiBold().FontSize(14);
                    col.Item().Text(ReportFormat.PeriodLabel(model)).FontSize(9).FontColor(ReportColors.NavyMuted);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Element(c => ComposeCurrencyBars(c, model.ByCurrency));

                    if (model.WeeklyTrend.Count > 0)
                        col.Item().PaddingTop(12).Element(c => ComposeWeeklySparkline(c, model.WeeklyTrend));

                    col.Item().PaddingTop(12).Text("Projects by margin").SemiBold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(1);
                            columns.ConstantColumn(40);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.ConstantColumn(44);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeader).Text("Project");
                            header.Cell().Element(CellHeader).Text("Client");
                            header.Cell().Element(CellHeader).Text("CCY");
                            header.Cell().Element(CellHeader).AlignRight().Text("Revenue");
                            header.Cell().Element(CellHeader).AlignRight().Text("Cost");
                            header.Cell().Element(CellHeader).AlignRight().Text("Margin");
                            header.Cell().Element(CellHeader).AlignRight().Text("%");
                        });

                        foreach (var project in model.Projects.Take(25))
                        {
                            // No red/green in this palette (see ReportColors) — Brand for
                            // positive margin, NavyMuted for negative reads as a de-emphasis
                            // rather than an alarm colour, which is deliberate here.
                            var marginColor = project.Margin < 0 ? ReportColors.NavyMuted : ReportColors.Brand;

                            table.Cell().Element(CellBody).Text(project.Name);
                            table.Cell().Element(CellBody).Text(project.ClientName);
                            table.Cell().Element(CellBody).Text(project.CurrencyCode);
                            table.Cell().Element(CellBody).AlignRight()
                                .Text(ReportFormat.Money(project.Revenue, project.CurrencyCode));
                            table.Cell().Element(CellBody).AlignRight()
                                .Text(ReportFormat.Money(project.CalculatedCost, project.CurrencyCode));
                            table.Cell().Element(CellBody).AlignRight()
                                .Text(ReportFormat.Money(project.Margin, project.CurrencyCode))
                                .FontColor(marginColor).SemiBold();
                            table.Cell().Element(CellBody).AlignRight()
                                .Text(project.MarginPct is { } pct ? ReportFormat.Percent(pct) : "—")
                                .FontColor(marginColor);
                        }
                    });

                    col.Item().PaddingTop(10).Element(c => PdfBasisBlock.Compose(c, ReportFormat.ProfitabilityBasisLines(model.Basis)));
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
            ReportFileNames.ForProfitability(ReportExportFormat.Pdf, model.GeneratedAtUtc));
    }

    private static IContainer CellHeader(IContainer container) =>
        container.DefaultTextStyle(x => x.SemiBold().FontSize(8)).Padding(2).BorderBottom(1).BorderColor(ReportColors.Canvas);

    private static IContainer CellBody(IContainer container) =>
        container.PaddingVertical(2).PaddingHorizontal(2);

    private static void ComposeCurrencyBars(IContainer container, IReadOnlyList<CurrencyFinancialKpisDto> currencies)
    {
        if (currencies.Count == 0) return;

        var maxTotal = currencies.Max(c => Math.Max(c.Revenue, c.Cost));

        container.Column(col =>
        {
            col.Item().Text("Revenue / Cost by currency").SemiBold().FontSize(11).FontColor(ReportColors.Navy);
            col.Item().PaddingTop(4).PaddingBottom(8).Height(1).Width(36).Background(ReportColors.Brand);

            for (var i = 0; i < currencies.Count; i++)
            {
                var ccy = currencies[i];
                var revRatio = maxTotal <= 0 ? 0f : (float)(ccy.Revenue / maxTotal);
                var costRatio = maxTotal <= 0 ? 0f : (float)(ccy.Cost / maxTotal);
                var fill = ReportColors.SeriesAt(i);

                col.Item().PaddingTop(6).Text($"{ccy.CurrencyCode}  ·  margin {ReportFormat.Money(ccy.Margin, ccy.CurrencyCode)}")
                    .FontSize(8).FontColor(ReportColors.Navy);

                col.Item().PaddingTop(2).Row(row =>
                {
                    row.ConstantItem(36).AlignMiddle().Text("Rev").FontSize(7).FontColor(ReportColors.NavyMuted);
                    row.RelativeItem().Height(10).Background(ReportColors.SurfaceMuted).Row(bar =>
                    {
                        if (revRatio > 0)
                            bar.RelativeItem(Math.Max(revRatio, 0.02f)).Background(fill);
                        if (revRatio < 1)
                            bar.RelativeItem(Math.Max(1f - revRatio, 0.001f));
                    });
                    row.ConstantItem(70).AlignRight().AlignMiddle()
                        .Text(ReportFormat.Money(ccy.Revenue, ccy.CurrencyCode))
                        .FontSize(7).FontColor(ReportColors.NavyMuted);
                });

                col.Item().PaddingTop(2).Row(row =>
                {
                    row.ConstantItem(36).AlignMiddle().Text("Cost").FontSize(7).FontColor(ReportColors.NavyMuted);
                    row.RelativeItem().Height(10).Background(ReportColors.SurfaceMuted).Row(bar =>
                    {
                        if (costRatio > 0)
                            bar.RelativeItem(Math.Max(costRatio, 0.02f)).Background(ReportColors.SeriesAt(i + 1));
                        if (costRatio < 1)
                            bar.RelativeItem(Math.Max(1f - costRatio, 0.001f));
                    });
                    row.ConstantItem(70).AlignRight().AlignMiddle()
                        .Text(ReportFormat.Money(ccy.Cost, ccy.CurrencyCode))
                        .FontSize(7).FontColor(ReportColors.NavyMuted);
                });
            }
        });
    }

    internal readonly record struct WeeklyMargin(DateOnly WeekStartDate, decimal Margin);

    internal sealed record CurrencyWeeklyMargins(string CurrencyCode, IReadOnlyList<WeeklyMargin> Weeks);

    /// <summary>
    /// Amounts are never summed across currencies (see the basis line of the same name) —
    /// grouping must key on (Week, Currency), not Week alone, or two currencies' margins
    /// collapse into one meaningless figure.
    /// </summary>
    internal static IReadOnlyList<CurrencyWeeklyMargins> GroupWeeklyMarginsByCurrency(
        IReadOnlyList<WeeklyFinancialTrendDto> trend) =>
        trend
            .GroupBy(t => t.CurrencyCode, StringComparer.Ordinal)
            .Select(g => new CurrencyWeeklyMargins(
                g.Key,
                g.GroupBy(t => t.WeekStartDate)
                    .Select(w => new WeeklyMargin(w.Key, w.Sum(t => t.Margin)))
                    .OrderBy(w => w.WeekStartDate)
                    .ToList()))
            .Where(c => c.Weeks.Count > 0)
            .OrderBy(c => c.CurrencyCode, StringComparer.Ordinal)
            .ToList();

    private static void ComposeWeeklySparkline(IContainer container, IReadOnlyList<WeeklyFinancialTrendDto> trend)
    {
        var byCurrency = GroupWeeklyMarginsByCurrency(trend);
        if (byCurrency.Count == 0) return;

        container.Column(col =>
        {
            col.Item().Text("Weekly margin trend").SemiBold().FontSize(11).FontColor(ReportColors.Navy);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            for (var i = 0; i < byCurrency.Count; i++)
            {
                if (i > 0)
                    col.Item().PaddingTop(10);

                var series = byCurrency[i];
                col.Item().Element(c => ComposeCurrencySparklineTrack(c, series.CurrencyCode, series.Weeks));
            }
        });
    }

    private static void ComposeCurrencySparklineTrack(
        IContainer container,
        string currencyCode,
        IReadOnlyList<WeeklyMargin> weeks)
    {
        var max = Math.Max(weeks.Max(w => Math.Abs(w.Margin)), 0.01m);
        // Normalise so the tallest bar fills ~90 % of the track.
        var trackHeight = 64f;

        container.Column(col =>
        {
            col.Item().Text(currencyCode).SemiBold().FontSize(8).FontColor(ReportColors.NavyMuted);

            col.Item().PaddingTop(2).Height(trackHeight + 12).Row(row =>
            {
                row.Spacing(2);
                foreach (var week in weeks)
                {
                    var ratio = (float)(week.Margin / max);
                    var barHeight = Math.Max(2f, Math.Abs(ratio) * trackHeight);
                    var fill = ratio >= 0 ? ReportColors.Brand : ReportColors.BrandDeep;

                    row.RelativeItem().AlignBottom().Height(barHeight).Background(fill);
                }
            });

            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text(ReportFormat.FriendlyWeek(weeks[0].WeekStartDate))
                    .FontSize(7).FontColor(ReportColors.NavyMuted);
                row.RelativeItem().AlignRight()
                    .Text(ReportFormat.FriendlyWeek(weeks[^1].WeekStartDate))
                    .FontSize(7).FontColor(ReportColors.NavyMuted);
            });
        });
    }
}

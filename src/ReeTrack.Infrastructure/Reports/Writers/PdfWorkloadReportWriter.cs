using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class PdfWorkloadReportWriter : IReportWriter<WorkloadReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Pdf;

    public ReportFile Write(WorkloadReportDto model)
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
                    col.Item().Text("ReeTrack Workload Report").SemiBold().FontSize(14);
                    col.Item().Text(ReportFormat.PeriodLabel(model)).FontSize(9).FontColor(ReportColors.NavyMuted);
                    col.Item().PaddingTop(4).Text(
                        $"{ReportFormat.HoursLabel(model.Kpis.TotalSeconds)} · " +
                        $"{model.Kpis.ActiveMembers} members · " +
                        $"{ReportFormat.Percent(model.Kpis.BillablePct)} billable")
                        .FontSize(8).FontColor(ReportColors.NavyMuted);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    if (model.Allocations.Count == 0)
                    {
                        col.Item().Text("No member hours in this range.").FontColor(ReportColors.NavyMuted);
                        return;
                    }

                    col.Item().Element(c => ComposeMemberHoursBars(c, model.Allocations));

                    if (model.Schedule.Count > 0)
                        col.Item().PaddingTop(12).Element(c => ComposeScheduleBars(c, model.Schedule));

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.3f);
                            columns.ConstantColumn(48);
                            columns.ConstantColumn(64);
                            columns.ConstantColumn(40);
                        });

                        table.Header(header =>
                        {
                            Header(header, "Member");
                            Header(header, "Client");
                            Header(header, "Project");
                            Header(header, "Hours", alignRight: true);
                            Header(header, "Billable hours", alignRight: true);
                            Header(header, "%", alignRight: true);
                        });

                        var i = 0;
                        foreach (var row in model.Allocations)
                        {
                            var zebra = i++ % 2 == 1;
                            Body(table, row.DisplayName, zebra);
                            Body(table, row.ClientName, zebra);
                            Body(table, row.ProjectName, zebra);
                            Body(table, ReportFormat.HoursLabel(row.TotalSeconds), zebra, alignRight: true);
                            Body(table, ReportFormat.HoursLabel(row.BillableSeconds), zebra, alignRight: true);
                            Body(table, ReportFormat.Percent(row.PctOfMemberTotal), zebra, alignRight: true);
                        }

                        // Grand total as a styled row inside the table, so it lines up
                        // under the Hours/Billable hours columns instead of a loose line.
                        Body(table, "Grand total", zebra: false, bold: true);
                        Body(table, "", zebra: false, bold: true);
                        Body(table, "", zebra: false, bold: true);
                        Body(table, ReportFormat.HoursLabel(model.GrandTotalSeconds), zebra: false, bold: true, alignRight: true);
                        Body(table, ReportFormat.HoursLabel(model.GrandTotalBillableSeconds), zebra: false, bold: true, alignRight: true);
                        Body(table, "", zebra: false, bold: true);
                    });

                    col.Item().PaddingTop(10).Element(c => PdfBasisBlock.Compose(c, ReportFormat.WorkloadBasisLines(model.Basis)));
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
            ReportFileNames.ForWorkload(ReportExportFormat.Pdf, model.GeneratedAtUtc));
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
        bool alignRight = false)
    {
        table.Cell().Element(c =>
        {
            var cell = c.PaddingVertical(3).PaddingHorizontal(3);
            if (zebra)
                cell = cell.Background(ReportColors.Canvas);
            if (bold)
                cell = cell.Background(ReportColors.BrandTint);
            if (alignRight)
                cell = cell.AlignRight();
            var text = cell.Text(value);
            if (bold)
                text.SemiBold();
        });
    }

    private static void ComposeMemberHoursBars(IContainer container, IReadOnlyList<WorkloadAllocationDto> allocations)
    {
        var byUser = allocations
            .GroupBy(a => a.UserId)
            .Select(g => new
            {
                DisplayName = g.First().DisplayName,
                TotalSeconds = g.Sum(a => a.TotalSeconds)
            })
            .OrderByDescending(u => u.TotalSeconds)
            .Take(10)
            .ToList();

        if (byUser.Count == 0) return;

        var max = byUser.Max(u => u.TotalSeconds);

        container.Column(col =>
        {
            col.Item().Text("Hours by member").SemiBold().FontSize(11).FontColor(ReportColors.Navy);
            col.Item().PaddingTop(4).PaddingBottom(8).Height(1).Width(36).Background(ReportColors.Brand);

            for (var i = 0; i < byUser.Count; i++)
            {
                var user = byUser[i];
                var ratio = max <= 0 ? 0f : (float)user.TotalSeconds / max;
                var color = i % 2 == 0 ? ReportColors.Brand : ReportColors.BrandHi;
                col.Item().PaddingVertical(3).Element(c => HorizontalBar(
                    c, user.DisplayName, ReportFormat.HoursLabel(user.TotalSeconds), ratio, color));
            }
        });
    }

    private static void ComposeScheduleBars(IContainer container, IReadOnlyList<WorkloadScheduleDto> schedule)
    {
        var items = schedule.Where(s => s.Hours > 0).ToList();
        if (items.Count == 0) return;

        var max = items.Max(s => s.Hours);

        container.Column(col =>
        {
            col.Item().Text("Schedule breakdown").SemiBold().FontSize(11).FontColor(ReportColors.Navy);
            col.Item().PaddingTop(4).PaddingBottom(8).Height(1).Width(36).Background(ReportColors.Brand);

            foreach (var item in items)
            {
                var ratio = max <= 0 ? 0f : (float)(item.Hours / max);
                var color = item.Label == "Overtime" ? ReportColors.Blue
                    : item.Label == "Weekend" ? ReportColors.BrandHi
                    : ReportColors.PurpleMid;
                col.Item().PaddingVertical(3).Element(c => HorizontalBar(
                    c, item.Label, $"{item.Hours:F1}h", ratio, color));
            }
        });
    }

    private static void HorizontalBar(
        IContainer container,
        string label,
        string value,
        float ratio,
        string fill)
    {
        ratio = Math.Clamp(ratio, 0f, 1f);
        container.Row(row =>
        {
            row.ConstantItem(78).AlignMiddle().Text(label).FontSize(8).FontColor(ReportColors.Navy);
            row.RelativeItem().Height(12).Background(ReportColors.SurfaceMuted).Row(bar =>
            {
                if (ratio > 0)
                    bar.RelativeItem(Math.Max(ratio, 0.02f)).Background(fill);
                if (ratio < 1)
                    bar.RelativeItem(Math.Max(1f - ratio, 0.001f));
            });
            row.ConstantItem(80).AlignRight().AlignMiddle()
                .Text(value).FontSize(8).FontColor(ReportColors.NavyMuted);
        });
    }
}

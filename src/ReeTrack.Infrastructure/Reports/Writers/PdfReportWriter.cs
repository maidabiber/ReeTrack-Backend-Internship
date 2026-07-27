using QuestPDF.Elements.Table;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class PdfReportWriter : IReportWriter
{
    static PdfReportWriter()
    {
        // Idempotent; Program.cs also sets this at API startup.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportExportFormat Format => ReportExportFormat.Pdf;

    public ReportFile Write(SummaryReportDto model)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(44);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(ReportColors.Navy));
                page.PageColor(ReportColors.White);

                page.Header().Element(h => ComposeHeader(h, model));
                page.Content().PaddingTop(18).Element(c => ComposeBody(c, model));
                page.Footer().PaddingTop(8).Element(ComposeFooter);
            });
        }).GeneratePdf();

        return new ReportFile(bytes, "application/pdf", ReportFileNames.For(ReportExportFormat.Pdf, model.GeneratedAtUtc));
    }

    /// <summary>
    /// Running header — repeats on every page, so it stays to the title and the period.
    /// Highlights belong to page 1 only and live in the body.
    /// </summary>
    private static void ComposeHeader(IContainer container, SummaryReportDto model)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text("ReeTrack Summary Report")
                    .SemiBold().FontSize(18).FontColor(ReportColors.Navy);
                row.ConstantItem(180).AlignRight().AlignMiddle()
                    .Text(ReportFormat.PeriodLabel(model))
                    .FontSize(8).FontColor(ReportColors.NavyMuted);
            });

            col.Item().PaddingTop(8).Height(2).Background(ReportColors.Brand);
        });
    }

    /// <summary>Page-1 preamble: provenance line + the highlights paragraph.</summary>
    private static void ComposeIntro(IContainer container, SummaryReportDto model)
    {
        container.Column(col =>
        {
            var generatedBy = string.IsNullOrWhiteSpace(model.GeneratedByName)
                ? ReportFormat.FriendlyDateTime(model.GeneratedAtUtc)
                : $"{ReportFormat.FriendlyDateTime(model.GeneratedAtUtc)} · by {model.GeneratedByName}";

            col.Item().Text(generatedBy).FontSize(7.5f).FontColor(ReportColors.NavyMuted);

            // Bulleted rather than one run-on paragraph: these are independent facts
            // and a reader scans them, they don't read them as prose.
            foreach (var line in ReportFormat.HighlightLines(model))
            {
                col.Item().PaddingTop(5).Row(row =>
                {
                    row.ConstantItem(10).Text("•").FontSize(9).FontColor(ReportColors.Brand);
                    row.RelativeItem().Text(line)
                        .FontSize(9).FontColor(ReportColors.NavyMuted).LineHeight(1.3f);
                });
            }
        });
    }

    private static void ComposeBody(IContainer container, SummaryReportDto model)
    {
        var kpis = model.Kpis;
        // Already ranked by ReportService — see the ordering contract on SummaryReportDto.
        var projects = model.Projects;
        var members = model.Members;

        container.Column(col =>
        {
            col.Spacing(22);

            col.Item().Element(c => ComposeIntro(c, model));
            col.Item().Element(c => ComposeKpiCards(c, kpis));

            col.Item().PaddingTop(4).Element(c => ComposeDayBars(c, model.Activity));
            col.Item().Element(c => ComposeBillableBar(c, kpis));
            col.Item().Element(c => ComposeScheduleInsights(c, model));
            col.Item().Element(c => ComposeCostInsights(c, model));
            col.Item().Element(c => ComposeProjectBars(c, projects.Take(8).ToList(), kpis.TotalSeconds));
            col.Item().Element(c => ComposeBudgetSection(c, projects));
            col.Item().Element(c => ComposeWeeklySparkline(c, model.WeeklyTrend));

            col.Item().PaddingTop(6).Element(c => ComposeProjectTable(c, projects, kpis.TotalSeconds, kpis.UnassignedSeconds));
            col.Item().Element(c => ComposeMemberTable(c, members, kpis.TotalSeconds));
            col.Item().Element(c => ComposeBasis(c, model));
        });
    }

    /// <summary>Closing "how these numbers were made" block — the audit trail for the figures.</summary>
    private static void ComposeBasis(IContainer container, SummaryReportDto model)
    {
        container.Column(col =>
        {
            col.Item().Text("Basis & assumptions").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(8)
                .Height(1).Width(36).Background(ReportColors.Brand);

            foreach (var line in ReportFormat.BasisLines(model))
            {
                col.Item().PaddingBottom(3).Row(row =>
                {
                    row.ConstantItem(10).Text("•").FontSize(7.5f).FontColor(ReportColors.NavyMuted);
                    row.RelativeItem().Text(line)
                        .FontSize(7.5f).FontColor(ReportColors.NavyMuted).LineHeight(1.3f);
                });
            }
        });
    }

    private static void ComposeKpiCards(IContainer container, ReportKpisDto kpis)
    {
        container.Row(row =>
        {
            row.Spacing(12);
            KpiCard(row, "Total hours", ReportFormat.HoursLabel(kpis.TotalSeconds));
            KpiCard(row, "Billable", ReportFormat.Percent(kpis.BillablePct));
            KpiCard(row, "Projects", kpis.ActiveProjects.ToString());
            KpiCard(row, "Members", kpis.ActiveMembers.ToString());
            KpiCard(row, "Entries", kpis.EntryCount.ToString());
        });

        static void KpiCard(RowDescriptor row, string label, string value)
        {
            row.RelativeItem().Background(ReportColors.SurfaceMuted).Border(1).BorderColor(ReportColors.HeaderGrayBg)
                .PaddingVertical(12).PaddingHorizontal(10).Column(card =>
                {
                    card.Item().Text(value).SemiBold().FontSize(14).FontColor(ReportColors.Navy);
                    card.Item().PaddingTop(4).Text(label).FontSize(7).FontColor(ReportColors.NavyMuted);
                });
        }
    }

    private static void ComposeScheduleInsights(IContainer container, SummaryReportDto model)
    {
        var byCurrency = SummaryReportAnalytics.CostByHourType(model);

        container.Column(col =>
        {
            col.Item().Text("Spend by hour type").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            if (byCurrency.Count == 0)
            {
                col.Item().Text("No cost data.").FontColor(ReportColors.NavyMuted);
                return;
            }

            col.Item().PaddingBottom(6)
                .Text("Normal = weekday regular. Weekend / holiday take the full entry cost. Weekday overtime is split by hour share. Never summed across currencies.")
                .FontSize(7).FontColor(ReportColors.NavyMuted).Italic();

            foreach (var insight in byCurrency)
            {
                col.Item().PaddingTop(8).Text(insight.CurrencyCode)
                    .SemiBold().FontSize(9).FontColor(ReportColors.Navy);

                var total = insight.TotalCost;
                var normalR = total <= 0 ? 0f : (float)(insight.NormalCost / total);
                var weekendR = total <= 0 ? 0f : (float)(insight.WeekendCost / total);
                var holidayR = total <= 0 ? 0f : (float)(insight.HolidayCost / total);
                var overtimeR = total <= 0 ? 0f : (float)(insight.OvertimeCost / total);

                col.Item().PaddingTop(6).Height(18).Row(row =>
                {
                    if (normalR > 0)
                        row.RelativeItem(normalR).Background(ReportColors.Brand);
                    if (weekendR > 0)
                        row.RelativeItem(weekendR).Background(ReportColors.BrandHi);
                    if (holidayR > 0)
                        row.RelativeItem(holidayR).Background(ReportColors.PurpleMid);
                    if (overtimeR > 0)
                        row.RelativeItem(overtimeR).Background(ReportColors.Blue);
                    if (total <= 0)
                        row.RelativeItem().Background(ReportColors.SurfaceMuted);
                });

                col.Item().PaddingTop(8).Row(row =>
                {
                    row.Spacing(14);
                    Legend(row, ReportColors.Brand, "Normal", insight.NormalCost, insight.CurrencyCode);
                    Legend(row, ReportColors.BrandHi, "Weekend", insight.WeekendCost, insight.CurrencyCode);
                    Legend(row, ReportColors.PurpleMid, "Holiday", insight.HolidayCost, insight.CurrencyCode);
                    Legend(row, ReportColors.Blue, "Overtime", insight.OvertimeCost, insight.CurrencyCode);
                });

                var maxSlice = Math.Max(
                    Math.Max(insight.NormalCost, insight.WeekendCost),
                    Math.Max(insight.HolidayCost, insight.OvertimeCost));

                col.Item().PaddingTop(10);
                CostBar(col, "Normal", insight.NormalCost, insight.CurrencyCode, maxSlice, ReportColors.Brand);
                CostBar(col, "Weekend", insight.WeekendCost, insight.CurrencyCode, maxSlice, ReportColors.BrandHi);
                CostBar(col, "Holiday", insight.HolidayCost, insight.CurrencyCode, maxSlice, ReportColors.PurpleMid);
                CostBar(col, "Overtime", insight.OvertimeCost, insight.CurrencyCode, maxSlice, ReportColors.Blue);

                col.Item().PaddingTop(6)
                    .Text($"Total  {ReportFormat.Money(insight.TotalCost, insight.CurrencyCode)}")
                    .SemiBold().FontSize(8).FontColor(ReportColors.Navy);
            }
        });

        static void Legend(RowDescriptor row, string color, string label, decimal amount, string currency)
        {
            row.AutoItem().Row(inner =>
            {
                inner.ConstantItem(8).Height(8).Background(color);
                inner.ConstantItem(4);
                inner.AutoItem().Text($"{label}  {ReportFormat.Money(amount, currency)}")
                    .FontSize(7).FontColor(ReportColors.NavyMuted);
            });
        }

        static void CostBar(
            ColumnDescriptor col,
            string label,
            decimal amount,
            string currency,
            decimal max,
            string color)
        {
            var ratio = max <= 0 ? 0f : (float)(amount / max);
            col.Item().PaddingVertical(3).Element(c => HorizontalBar(
                c, label, ReportFormat.Money(amount, currency), ratio, color));
        }
    }

    private static void ComposeDayBars(IContainer container, IReadOnlyList<DayOfWeekHoursDto> activity)
    {
        var max = activity.Count == 0 ? 0L : activity.Max(d => d.TotalSeconds);
        container.Column(col =>
        {
            col.Item().Text("Hours by day").SemiBold().FontSize(11).FontColor(ReportColors.Navy);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            if (activity.Count == 0)
            {
                col.Item().Text("No activity.").FontColor(ReportColors.NavyMuted);
                return;
            }

            for (var i = 0; i < activity.Count; i++)
            {
                var day = activity[i];
                var ratio = max <= 0 ? 0f : (float)day.TotalSeconds / max;
                // Alternate the same brand blue / purple only.
                var color = i % 2 == 0 ? ReportColors.Brand : ReportColors.BrandHi;
                col.Item().PaddingVertical(4).Element(c => HorizontalBar(
                    c, day.DayOfWeek, ReportFormat.HoursLabel(day.TotalSeconds), ratio, color));
            }
        });
    }

    private static void ComposeCostInsights(IContainer container, SummaryReportDto model)
    {
        var byCurrency = SummaryReportAnalytics.CostByCurrency(model);
        container.Column(col =>
        {
            col.Item().Text("Cost insights").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            if (byCurrency.Count == 0)
            {
                col.Item().Text("No project cost data.").FontColor(ReportColors.NavyMuted);
                return;
            }

            col.Item().PaddingBottom(6)
                .Text("Totals stay per currency — amounts are never summed across codes.")
                .FontSize(7).FontColor(ReportColors.NavyMuted).Italic();

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(0.7f);
                    c.RelativeColumn(0.7f);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(2.0f);
                });

                Header(table, "Currency");
                Header(table, "Projects");
                Header(table, "Total cost");
                Header(table, "Avg / h");
                Header(table, "Highest");

                var i = 0;
                foreach (var row in byCurrency)
                {
                    var zebra = i++ % 2 == 1;
                    Body(table, row.CurrencyCode, zebra);
                    Body(table, row.ProjectCount.ToString(), zebra, alignRight: true);
                    Body(table, ReportFormat.Money(row.TotalCost, row.CurrencyCode), zebra, alignRight: true);
                    Body(table,
                        row.AvgCostPerHour > 0
                            ? ReportFormat.Money(row.AvgCostPerHour, row.CurrencyCode)
                            : "—",
                        zebra,
                        alignRight: true);
                    Body(table,
                        $"{Truncate(row.TopProjectName, 18)} ({ReportFormat.Money(row.TopProjectCost, row.CurrencyCode)})",
                        zebra);
                }
            });
        });
    }

    private static void ComposeBillableBar(IContainer container, ReportKpisDto kpis)
    {
        var total = kpis.BillableSeconds + kpis.NonBillableSeconds;
        var billableRatio = total <= 0 ? 0f : (float)kpis.BillableSeconds / total;
        var nonBillableRatio = total <= 0 ? 0f : (float)kpis.NonBillableSeconds / total;

        container.Column(col =>
        {
            col.Item().Text("Billable vs non-billable").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            col.Item().Height(18).Row(row =>
            {
                if (billableRatio > 0)
                    row.RelativeItem(billableRatio).Background(ReportColors.Billable);
                if (nonBillableRatio > 0)
                    row.RelativeItem(nonBillableRatio).Background(ReportColors.NonBillable);
                if (total == 0)
                    row.RelativeItem().Background(ReportColors.SurfaceMuted);
            });

            col.Item().PaddingTop(8).Row(row =>
            {
                row.AutoItem().Text($"● Billable  {ReportFormat.HoursLabel(kpis.BillableSeconds)}")
                    .FontSize(8).FontColor(ReportColors.Billable);
                row.ConstantItem(20);
                row.AutoItem().Text($"● Non-billable  {ReportFormat.HoursLabel(kpis.NonBillableSeconds)}")
                    .FontSize(8).FontColor(ReportColors.NonBillable);
            });
        });
    }

    private static void ComposeProjectBars(
        IContainer container,
        IReadOnlyList<ProjectSummaryDto> topProjects,
        long totalSeconds)
    {
        var max = topProjects.Count == 0 ? 0L : topProjects.Max(p => p.TotalSeconds);
        container.Column(col =>
        {
            col.Item().Text("Hours by project (top 8)").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            if (topProjects.Count == 0)
            {
                col.Item().Text("No project-linked time.").FontColor(ReportColors.NavyMuted);
                return;
            }

            for (var i = 0; i < topProjects.Count; i++)
            {
                var p = topProjects[i];
                var ratio = max <= 0 ? 0f : (float)p.TotalSeconds / max;
                var right = $"{ReportFormat.HoursLabel(p.TotalSeconds)} · {ReportFormat.Money(p.CalculatedCost, p.CurrencyCode)}";
                col.Item().PaddingVertical(4).Element(c => HorizontalBar(
                    c, Truncate(p.Name, 22), right, ratio,
                    i % 2 == 0 ? ReportColors.Brand : ReportColors.BrandHi));
            }
        });
    }

    private static void ComposeBudgetSection(
        IContainer container,
        IReadOnlyList<ProjectSummaryDto> projects)
    {
        var estimated = projects
            .Where(p => p.TimeEstimateHours is > 0m)
            .OrderByDescending(p => SummaryReportAnalytics.EstimateUsedPct(p.TotalSeconds, p.TimeEstimateHours) ?? 0m)
            .Take(8)
            .ToList();
        var fixedFee = projects
            .Where(p => p.FixedFeeAmount is > 0m)
            .OrderByDescending(p => p.FixedFeeAmount ?? 0m)
            .Take(6)
            .ToList();

        if (estimated.Count == 0 && fixedFee.Count == 0)
            return;

        container.Column(col =>
        {
            col.Item().Text("Budget & estimates").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            if (estimated.Count > 0)
            {
                col.Item().PaddingBottom(4)
                    .Text("Time estimate used (actual ÷ estimate — purple = over budget)")
                    .FontSize(7).FontColor(ReportColors.NavyMuted).Italic();

                foreach (var p in estimated)
                {
                    var used = SummaryReportAnalytics.EstimateUsedPct(p.TotalSeconds, p.TimeEstimateHours) ?? 0m;
                    var ratio = (float)(used / 100m);
                    var over = used > 100m;
                    var right =
                        $"{ReportFormat.HoursLabel(p.TotalSeconds)} / {ReportFormat.Hours2(p.TimeEstimateHours!.Value)}h ({ReportFormat.Percent(used)})";
                    col.Item().PaddingVertical(4).Element(c => HorizontalBar(
                        c, Truncate(p.Name, 22), right, ratio,
                        over ? ReportColors.PurpleDeep : ReportColors.Brand));
                }
            }

            if (fixedFee.Count > 0)
            {
                col.Item().PaddingTop(estimated.Count > 0 ? 10 : 0).PaddingBottom(4)
                    .Text("Fixed-fee margin (fee − labour cost)")
                    .FontSize(7).FontColor(ReportColors.NavyMuted).Italic();

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(1.3f);
                        c.RelativeColumn(1.3f);
                        c.RelativeColumn(1.3f);
                    });

                    Header(table, "Project");
                    Header(table, "Fixed fee");
                    Header(table, "Cost");
                    Header(table, "Margin");

                    var i = 0;
                    foreach (var p in fixedFee)
                    {
                        var zebra = i++ % 2 == 1;
                        var margin = SummaryReportAnalytics.FixedFeeMargin(p.FixedFeeAmount, p.CalculatedCost) ?? 0m;
                        Body(table, p.Name, zebra);
                        Body(table, ReportFormat.Money(p.FixedFeeAmount!.Value, p.CurrencyCode), zebra, alignRight: true);
                        Body(table, ReportFormat.Money(p.CalculatedCost, p.CurrencyCode), zebra, alignRight: true);
                        BudgetMarginCell(table, margin, p.CurrencyCode, zebra);
                    }
                });
            }
        });
    }

    private static void BudgetMarginCell(TableDescriptor table, decimal margin, string currency, bool zebra)
    {
        table.Cell().Element(c => c
            .Background(zebra ? ReportColors.SurfaceMuted : ReportColors.White)
            .BorderBottom(0.5f).BorderColor(ReportColors.Canvas)
            .PaddingVertical(5).PaddingHorizontal(6)
            .AlignRight()
            .Text(ReportFormat.Money(margin, currency))
            .FontSize(8).SemiBold()
            .FontColor(margin < 0 ? ReportColors.PurpleDeep : ReportColors.Navy));
    }

    private static void ComposeWeeklySparkline(IContainer container, IReadOnlyList<TrendPointDto> trend)
    {
        var max = trend.Count == 0 ? 0L : trend.Max(t => t.TotalSeconds);
        container.Column(col =>
        {
            col.Item().Text("Weekly trend").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            if (trend.Count == 0)
            {
                col.Item().Text("No weekly data.").FontColor(ReportColors.NavyMuted);
                return;
            }

            col.Item().Height(56).Row(row =>
            {
                row.Spacing(3);
                var i = 0;
                foreach (var week in trend)
                {
                    var ratio = max <= 0 ? 0f : (float)week.TotalSeconds / max;
                    var barHeight = Math.Max(2f, ratio * 56f);
                    // Alternate brand blue / purple along the sparkline.
                    var fill = i++ % 2 == 0 ? ReportColors.Brand : ReportColors.BrandHi;
                    row.RelativeItem().AlignBottom().Height(barHeight).Background(fill);
                }
            });

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text(ReportFormat.FriendlyWeek(trend[0].WeekStartDate))
                    .FontSize(7).FontColor(ReportColors.NavyMuted);
                row.RelativeItem().AlignRight()
                    .Text(ReportFormat.FriendlyWeek(trend[^1].WeekStartDate))
                    .FontSize(7).FontColor(ReportColors.NavyMuted);
            });
        });
    }

    private static void ComposeProjectTable(
        IContainer container,
        IReadOnlyList<ProjectSummaryDto> projects,
        long totalSeconds,
        long unassignedSeconds)
    {
        container.Column(col =>
        {
            col.Item().Text("By project").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.6f);
                    c.RelativeColumn(1.0f);
                    c.RelativeColumn(1.0f);
                    c.RelativeColumn(1.3f);
                    c.RelativeColumn(0.7f);
                });

                // table.Header repeats the column labels on every page the table spills onto.
                table.Header(header =>
                {
                    Header(header, "Project");
                    Header(header, "Client");
                    Header(header, "Hours");
                    Header(header, "Est. used");
                    Header(header, "Cost");
                    Header(header, "%");
                });

                var i = 0;
                foreach (var p in projects)
                {
                    var zebra = i++ % 2 == 1;
                    var used = SummaryReportAnalytics.EstimateUsedPct(p.TotalSeconds, p.TimeEstimateHours);
                    Body(table, p.Name, zebra);
                    Body(table, string.IsNullOrWhiteSpace(p.ClientName) ? "—" : p.ClientName, zebra);
                    Body(table, ReportFormat.HoursLabel(p.TotalSeconds), zebra, alignRight: true);
                    Body(table, used is { } u ? ReportFormat.Percent(u) : "—", zebra, alignRight: true);
                    Body(table, ReportFormat.Money(p.CalculatedCost, p.CurrencyCode), zebra, alignRight: true);
                    Body(table, ReportFormat.Percent(SummaryReportAnalytics.PctOfTotal(p.TotalSeconds, totalSeconds)), zebra, alignRight: true);
                }

                // Time logged against no project — without it the rows never reach 100%.
                if (unassignedSeconds > 0)
                {
                    var zebra = i++ % 2 == 1;
                    Body(table, ReportFormat.UnassignedLabel, zebra);
                    Body(table, "—", zebra);
                    Body(table, ReportFormat.HoursLabel(unassignedSeconds), zebra, alignRight: true);
                    Body(table, "—", zebra, alignRight: true);
                    Body(table, "—", zebra, alignRight: true);
                    Body(table, ReportFormat.Percent(SummaryReportAnalytics.PctOfTotal(unassignedSeconds, totalSeconds)), zebra, alignRight: true);
                }

                // Portfolio total, not the sum of project rows: the two differ by unassigned time.
                Body(table, "Total", bold: true);
                Body(table, "", bold: true);
                Body(table, ReportFormat.HoursLabel(totalSeconds), bold: true, alignRight: true);
                Body(table, "", bold: true, alignRight: true);
                Body(table, "—", bold: true, alignRight: true); // multi-currency: never sum cost
                Body(table, totalSeconds > 0 ? "100%" : "—", bold: true, alignRight: true);
            });
        });
    }

    private static void ComposeMemberTable(
        IContainer container,
        IReadOnlyList<MemberHoursDto> members,
        long totalSeconds)
    {
        container.Column(col =>
        {
            col.Item().Text("By member").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(10)
                .Height(1).Width(36).Background(ReportColors.Brand);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2.4f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(0.8f);
                });

                table.Header(header =>
                {
                    Header(header, "Member");
                    Header(header, "Hours");
                    Header(header, "%");
                });

                var i = 0;
                foreach (var m in members)
                {
                    var zebra = i++ % 2 == 1;
                    Body(table, m.DisplayName, zebra);
                    Body(table, ReportFormat.HoursLabel(m.TotalSeconds), zebra, alignRight: true);
                    Body(table, ReportFormat.Percent(SummaryReportAnalytics.PctOfTotal(m.TotalSeconds, totalSeconds)), zebra, alignRight: true);
                }

                Body(table, "Total", bold: true);
                Body(table, ReportFormat.HoursLabel(members.Sum(m => m.TotalSeconds)), bold: true, alignRight: true);
                Body(table, "100%", bold: true, alignRight: true);
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().DefaultTextStyle(x => x.FontSize(8).FontColor(ReportColors.NavyMuted))
            .Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
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
            row.ConstantItem(110).AlignRight().AlignMiddle()
                .Text(value).FontSize(8).FontColor(ReportColors.NavyMuted);
        });
    }

    // table.Cell() and table.Header(h => h.Cell()) both yield an ITableCellContainer,
    // so the styling lives there once and each descriptor gets a thin overload.
    private static void Header(TableDescriptor table, string text) =>
        HeaderCell(table.Cell(), text);

    private static void Header(TableCellDescriptor header, string text) =>
        HeaderCell(header.Cell(), text);

    private static void HeaderCell(ITableCellContainer cell, string text) =>
        cell.Element(c => c
            .Background(ReportColors.HeaderGrayBg)
            .PaddingVertical(6).PaddingHorizontal(6)
            .Text(text).SemiBold().FontSize(8).FontColor(ReportColors.HeaderGray));

    private static void Body(
        TableDescriptor table,
        string text,
        bool zebra = false,
        bool bold = false,
        bool alignRight = false)
    {
        table.Cell().Element(c =>
        {
            var cell = c
                .Background(zebra ? ReportColors.SurfaceMuted : ReportColors.White)
                .BorderBottom(0.5f).BorderColor(ReportColors.Canvas)
                .PaddingVertical(5).PaddingHorizontal(6);
            if (alignRight)
                cell = cell.AlignRight();
            var t = cell.Text(text).FontSize(8).FontColor(ReportColors.Navy);
            if (bold)
                t.SemiBold();
        });
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}

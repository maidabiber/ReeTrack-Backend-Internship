using ClosedXML.Excel;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;
using static ReeTrack.Infrastructure.Reports.Writers.ExcelReportStyles;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class ExcelReportWriter : IReportWriter<SummaryReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Xlsx;

    public ReportFile Write(SummaryReportDto model)
    {
        using var workbook = new XLWorkbook();

        WriteOverview(workbook, model);
        WriteDayOfWeek(workbook, model);
        WriteByProject(workbook, model);
        WriteByMember(workbook, model);
        WriteByWeek(workbook, model);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportFile(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReportFileNames.For(ReportExportFormat.Xlsx, model.GeneratedAtUtc));
    }

    private static void WriteOverview(XLWorkbook workbook, SummaryReportDto model)
    {
        var ws = workbook.Worksheets.Add("Overview");
        ws.TabColor = XLColor.FromHtml(ReportColors.Brand);

        ws.Cell(1, 1).Value = "ReeTrack Summary Report";
        ws.Range(1, 1, 1, 2).Merge();
        StyleTitle(ws.Cell(1, 1));

        ws.Cell(2, 1).Value = "Period";
        ws.Cell(2, 2).Value = ReportFormat.PeriodLabel(model);
        StyleMuted(ws.Cell(2, 1));
        StyleMuted(ws.Cell(2, 2));

        ws.Cell(3, 1).Value = "Generated";
        ws.Cell(3, 2).Value = ReportFormat.FriendlyDateTime(model.GeneratedAtUtc);
        StyleMuted(ws.Cell(3, 1));
        StyleMuted(ws.Cell(3, 2));

        ws.Cell(5, 1).Value = "Highlights";
        StyleHeaderBand(ws.Range(5, 1, 5, 2));
        ws.Cell(6, 1).Value = ReportFormat.Highlights(model);
        ws.Range(6, 1, 6, 2).Merge();
        ws.Cell(6, 1).Style.Alignment.WrapText = true;
        ws.Row(6).Height = 48;

        ws.Cell(8, 1).Value = "KPI";
        ws.Cell(8, 2).Value = "Value";
        StyleHeaderBand(ws.Range(8, 1, 8, 2));

        var kpis = model.Kpis;
        var row = 9;
        void Kpi(string label, XLCellValue value, string? numberFormat = null)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            if (numberFormat is not null)
                ws.Cell(row, 2).Style.NumberFormat.Format = numberFormat;
            if (row % 2 == 0)
                ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml(ReportColors.SurfaceMuted);
            row++;
        }

        Kpi("Total hours", (double)ReportFormat.Hours(kpis.TotalSeconds), "0.00");
        Kpi("Billable hours", (double)ReportFormat.Hours(kpis.BillableSeconds), "0.00");
        Kpi("Non-billable hours", (double)ReportFormat.Hours(kpis.NonBillableSeconds), "0.00");
        Kpi("Billable %", (double)(kpis.BillablePct / 100m), "0.0%");
        Kpi("Entries", kpis.EntryCount);
        Kpi("Active members", kpis.ActiveMembers);
        Kpi("Active projects", kpis.ActiveProjects);
        Kpi("Overtime hours", (double)kpis.OvertimeHours, "0.00");
        Kpi("Weekend hours", (double)kpis.WeekendHours, "0.00");
        Kpi("Holiday hours", (double)kpis.HolidayHours, "0.00");
        Kpi("Unassigned hours", (double)ReportFormat.Hours(kpis.UnassignedSeconds), "0.00");

        // Real numeric columns, not one text cell per row: the point of the xlsx is that
        // these can be sorted, charted and totalled.
        row += 2;
        ws.Cell(row, 1).Value = "Cost by currency";
        ws.Cell(row, 2).Value = "Projects";
        ws.Cell(row, 3).Value = "Total cost";
        ws.Cell(row, 4).Value = "Avg / h";
        ws.Cell(row, 5).Value = "Top project";
        ws.Cell(row, 6).Value = "Top project cost";
        StyleHeaderBand(ws.Range(row, 1, row, 6));
        row++;

        foreach (var insight in SummaryReportAnalytics.CostByCurrency(model))
        {
            var money = CurrencyFormat(insight.CurrencyCode);
            ws.Cell(row, 1).Value = insight.CurrencyCode;
            ws.Cell(row, 2).Value = insight.ProjectCount;
            ws.Cell(row, 3).Value = (double)insight.TotalCost;
            ws.Cell(row, 3).Style.NumberFormat.Format = money;
            ws.Cell(row, 4).Value = (double)insight.AvgCostPerHour;
            ws.Cell(row, 4).Style.NumberFormat.Format = money;
            ws.Cell(row, 5).Value = insight.TopProjectName;
            ws.Cell(row, 6).Value = (double)insight.TopProjectCost;
            ws.Cell(row, 6).Style.NumberFormat.Format = money;
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 6));
            row++;
        }

        row += 2;
        ws.Cell(row, 1).Value = "Spend by hour type";
        ws.Cell(row, 2).Value = "Normal";
        ws.Cell(row, 3).Value = "Weekend";
        ws.Cell(row, 4).Value = "Holiday";
        ws.Cell(row, 5).Value = "Overtime";
        ws.Cell(row, 6).Value = "Total";
        StyleHeaderBand(ws.Range(row, 1, row, 6));
        row++;

        foreach (var insight in SummaryReportAnalytics.CostByHourType(model))
        {
            var money = CurrencyFormat(insight.CurrencyCode);
            ws.Cell(row, 1).Value = insight.CurrencyCode;
            ws.Cell(row, 2).Value = (double)insight.NormalCost;
            ws.Cell(row, 3).Value = (double)insight.WeekendCost;
            ws.Cell(row, 4).Value = (double)insight.HolidayCost;
            ws.Cell(row, 5).Value = (double)insight.OvertimeCost;
            ws.Cell(row, 6).Value = (double)insight.TotalCost;
            ws.Range(row, 2, row, 6).Style.NumberFormat.Format = money;
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 6));
            row++;
        }

        row += 2;
        ws.Cell(row, 1).Value = "Basis & assumptions";
        StyleHeaderBand(ws.Range(row, 1, row, 6));
        row++;

        foreach (var line in ReportFormat.SummaryBasisLines(model))
        {
            ws.Cell(row, 1).Value = line;
            ws.Range(row, 1, row, 6).Merge();
            StyleMuted(ws.Cell(row, 1));
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        // Set outright rather than Math.Max: the merged basis sentences would otherwise
        // drag column 1 to their full width.
        ws.Column(1).Width = 26;
        ws.Column(2).Width = Math.Max(ws.Column(2).Width, 14);
    }

    private static void WriteDayOfWeek(XLWorkbook workbook, SummaryReportDto model)
    {
        var ws = workbook.Worksheets.Add("Day of week");
        ws.TabColor = XLColor.FromHtml(ReportColors.Blue);

        ws.Cell(1, 1).Value = "Day";
        ws.Cell(1, 2).Value = "Hours";
        ws.Cell(1, 3).Value = "% of total";
        StyleHeaderBand(ws.Range(1, 1, 1, 3));

        var total = model.Kpis.TotalSeconds;
        var row = 2;
        foreach (var day in model.Activity)
        {
            ws.Cell(row, 1).Value = day.DayOfWeek;
            ws.Cell(row, 2).Value = (double)ReportFormat.Hours(day.TotalSeconds);
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 3).Value = (double)(SummaryReportAnalytics.PctOfTotal(day.TotalSeconds, total) / 100m);
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 3));
            row++;
        }

        if (row > 2)
            AddDataBars(ws.Range(2, 2, row - 1, 2));

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void WriteByProject(XLWorkbook workbook, SummaryReportDto model)
    {
        var ws = workbook.Worksheets.Add("By project");
        ws.TabColor = XLColor.FromHtml(ReportColors.BrandHi);

        const int idCol = 20;
        ws.Cell(1, 1).Value = "Project";
        ws.Cell(1, 2).Value = "Client";
        ws.Cell(1, 3).Value = "Status";
        ws.Cell(1, 4).Value = "Hours";
        ws.Cell(1, 5).Value = "% of total";
        ws.Cell(1, 6).Value = "Est. hours";
        ws.Cell(1, 7).Value = "Est. used";
        ws.Cell(1, 8).Value = "Billing";
        ws.Cell(1, 9).Value = "Hourly rate";
        ws.Cell(1, 10).Value = "Fixed fee";
        ws.Cell(1, 11).Value = "Cost";
        ws.Cell(1, 12).Value = "Margin";
        ws.Cell(1, 13).Value = "Normal cost";
        ws.Cell(1, 14).Value = "Weekend cost";
        ws.Cell(1, 15).Value = "Holiday cost";
        ws.Cell(1, 16).Value = "Overtime cost";
        ws.Cell(1, 17).Value = "Overtime h";
        ws.Cell(1, 18).Value = "Weekend h";
        ws.Cell(1, 19).Value = "Holiday h";
        ws.Cell(1, idCol).Value = "Project ID";
        StyleHeaderBand(ws.Range(1, 1, 1, idCol));

        var projects = model.Projects;
        var total = model.Kpis.TotalSeconds;
        var row = 2;

        foreach (var p in projects)
        {
            var money = CurrencyFormat(p.CurrencyCode);
            var estimateUsed = SummaryReportAnalytics.EstimateUsedPct(p.TotalSeconds, p.TimeEstimateHours);
            var margin = SummaryReportAnalytics.FixedFeeMargin(p.FixedFeeAmount, p.CalculatedCost);

            ws.Cell(row, 1).Value = p.Name;
            ws.Cell(row, 2).Value = p.ClientName;
            ws.Cell(row, 3).Value = p.Status;
            ws.Cell(row, 4).Value = (double)ReportFormat.Hours(p.TotalSeconds);
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 5).Value = (double)(SummaryReportAnalytics.PctOfTotal(p.TotalSeconds, total) / 100m);
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.0%";
            if (p.TimeEstimateHours is { } est)
            {
                ws.Cell(row, 6).Value = (double)est;
                ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
            }
            if (estimateUsed is { } used)
            {
                ws.Cell(row, 7).Value = (double)(used / 100m);
                ws.Cell(row, 7).Style.NumberFormat.Format = "0.0%";
                if (used > 100m)
                    ws.Cell(row, 7).Style.Font.FontColor = XLColor.FromHtml(ReportColors.BrandDeep);
            }
            ws.Cell(row, 8).Value = ReportFormat.BillingModelLabel(p.HourlyRate, p.FixedFeeAmount);
            if (p.HourlyRate is { } hr)
            {
                ws.Cell(row, 9).Value = (double)hr;
                ws.Cell(row, 9).Style.NumberFormat.Format = money;
            }
            if (p.FixedFeeAmount is { } ff)
            {
                ws.Cell(row, 10).Value = (double)ff;
                ws.Cell(row, 10).Style.NumberFormat.Format = money;
            }
            ws.Cell(row, 11).Value = (double)p.CalculatedCost;
            ws.Cell(row, 11).Style.NumberFormat.Format = money;
            if (margin is { } mg)
            {
                ws.Cell(row, 12).Value = (double)mg;
                ws.Cell(row, 12).Style.NumberFormat.Format = money;
                ws.Cell(row, 12).Style.Font.FontColor =
                    XLColor.FromHtml(mg < 0 ? ReportColors.BrandDeep : ReportColors.Navy);
            }
            ws.Cell(row, 13).Value = (double)p.NormalCost;
            ws.Cell(row, 13).Style.NumberFormat.Format = money;
            ws.Cell(row, 14).Value = (double)p.WeekendCost;
            ws.Cell(row, 14).Style.NumberFormat.Format = money;
            ws.Cell(row, 15).Value = (double)p.HolidayCost;
            ws.Cell(row, 15).Style.NumberFormat.Format = money;
            ws.Cell(row, 16).Value = (double)p.OvertimeCost;
            ws.Cell(row, 16).Style.NumberFormat.Format = money;
            ws.Cell(row, 17).Value = (double)p.OvertimeHours;
            ws.Cell(row, 17).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 18).Value = (double)p.WeekendHours;
            ws.Cell(row, 18).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 19).Value = (double)p.HolidayHours;
            ws.Cell(row, 19).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, idCol).Value = p.ProjectId.ToString();
            StyleMuted(ws.Cell(row, idCol));
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, idCol));
            row++;
        }

        // Time with no project: without this row the sheet's rows do not add up to
        // the Total, and the 100% below it would be a lie.
        var unassigned = model.Kpis.UnassignedSeconds;
        if (unassigned > 0)
        {
            ws.Cell(row, 1).Value = ReportFormat.UnassignedLabel;
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Cell(row, 4).Value = (double)ReportFormat.Hours(unassigned);
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 5).Value = (double)(SummaryReportAnalytics.PctOfTotal(unassigned, total) / 100m);
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.0%";
            ws.Cell(row, idCol).Value = "—";
            StyleMuted(ws.Cell(row, idCol));
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, idCol));
            row++;
        }

        if (projects.Count > 0 || unassigned > 0)
        {
            var lastData = row - 1;
            AddDataBars(ws.Range(2, 4, lastData, 4));   // Hours
            AddDataBars(ws.Range(2, 7, lastData, 7));   // Est. used
            AddDataBars(ws.Range(2, 11, lastData, 11)); // Cost

            ws.Cell(row, 1).Value = "Total";
            ws.Cell(row, 1).Style.Font.Bold = true;
            // Portfolio total, not the sum of project rows — the two differ by unassigned time.
            ws.Cell(row, 4).Value = (double)ReportFormat.Hours(total);
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 5).Value = total > 0 ? 1d : 0d;
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.0%";
            ws.Cell(row, 5).Style.Font.Bold = true;
            var estimateTotal = projects.Sum(p => p.TimeEstimateHours ?? 0m);
            if (estimateTotal > 0m)
            {
                ws.Cell(row, 6).Value = (double)estimateTotal;
                ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 6).Style.Font.Bold = true;
            }
            // Multi-currency: never sum money columns.
            foreach (var moneyCol in new[] { 9, 10, 11, 12, 13, 14, 15, 16 })
                ws.Cell(row, moneyCol).Value = "—";
            ws.Cell(row, 17).Value = (double)projects.Sum(p => p.OvertimeHours);
            ws.Cell(row, 17).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 17).Style.Font.Bold = true;
            ws.Cell(row, 18).Value = (double)projects.Sum(p => p.WeekendHours);
            ws.Cell(row, 18).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 18).Style.Font.Bold = true;
            ws.Cell(row, 19).Value = (double)projects.Sum(p => p.HolidayHours);
            ws.Cell(row, 19).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 19).Style.Font.Bold = true;
            ws.Range(row, 1, row, idCol).Style.Fill.BackgroundColor = XLColor.FromHtml(ReportColors.BrandTint);
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        ws.Column(idCol).Hide();
    }

    private static void WriteByMember(XLWorkbook workbook, SummaryReportDto model)
    {
        var ws = workbook.Worksheets.Add("By member");
        ws.TabColor = XLColor.FromHtml(ReportColors.PurpleMid);

        ws.Cell(1, 1).Value = "Member";
        ws.Cell(1, 2).Value = "Hours";
        ws.Cell(1, 3).Value = "% of total";
        ws.Cell(1, 4).Value = "User ID";
        StyleHeaderBand(ws.Range(1, 1, 1, 4));

        var members = model.Members;
        var total = model.Kpis.TotalSeconds;
        var row = 2;

        foreach (var m in members)
        {
            ws.Cell(row, 1).Value = m.DisplayName;
            ws.Cell(row, 2).Value = (double)ReportFormat.Hours(m.TotalSeconds);
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 3).Value = (double)(SummaryReportAnalytics.PctOfTotal(m.TotalSeconds, total) / 100m);
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
            ws.Cell(row, 4).Value = m.UserId.ToString();
            StyleMuted(ws.Cell(row, 4));
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 4));
            row++;
        }

        if (members.Count > 0)
        {
            var lastData = row - 1;
            AddDataBars(ws.Range(2, 2, lastData, 2));

            ws.Cell(row, 1).Value = "Total";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = (double)ReportFormat.Hours(members.Sum(m => m.TotalSeconds));
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = 1d;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml(ReportColors.BrandTint);
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        ws.Column(4).Hide();
    }

    private static void WriteByWeek(XLWorkbook workbook, SummaryReportDto model)
    {
        var ws = workbook.Worksheets.Add("By week");
        ws.TabColor = XLColor.FromHtml(ReportColors.BrandDeep);

        ws.Cell(1, 1).Value = "Week start";
        ws.Cell(1, 2).Value = "Hours";
        StyleHeaderBand(ws.Range(1, 1, 1, 2));

        var row = 2;
        foreach (var week in model.WeeklyTrend)
        {
            // A real date cell: sortable, chartable, and unambiguous when the 26-week
            // window crosses a year boundary. A "13 Jul" string is none of those.
            ws.Cell(row, 1).Value = week.WeekStartDate.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, 1).Style.NumberFormat.Format = "dd mmm yyyy";
            ws.Cell(row, 2).Value = (double)ReportFormat.Hours(week.TotalSeconds);
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 2));
            row++;
        }

        if (row > 2)
            AddDataBars(ws.Range(2, 2, row - 1, 2));

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }
}

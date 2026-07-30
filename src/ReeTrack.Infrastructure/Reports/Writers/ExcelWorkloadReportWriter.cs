using ClosedXML.Excel;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using static ReeTrack.Infrastructure.Reports.Writers.ExcelReportStyles;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class ExcelWorkloadReportWriter : IReportWriter<WorkloadReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Xlsx;

    public ReportFile Write(WorkloadReportDto model)
    {
        using var workbook = new XLWorkbook();
        WriteOverview(workbook, model);
        WriteAllocations(workbook, model);
        WriteSchedule(workbook, model);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportFile(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReportFileNames.ForWorkload(ReportExportFormat.Xlsx, model.GeneratedAtUtc));
    }

    private static void WriteOverview(XLWorkbook workbook, WorkloadReportDto model)
    {
        var ws = workbook.Worksheets.Add("Overview");
        ws.TabColor = XLColor.FromHtml(ReportColors.Brand);

        ws.Cell(1, 1).Value = "ReeTrack Workload Report";
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

        ws.Cell(5, 1).Value = "KPI";
        ws.Cell(5, 2).Value = "Value";
        StyleHeaderBand(ws.Range(5, 1, 5, 2));

        var row = 6;
        void Kpi(string label, XLCellValue value, string? numberFormat = null)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            if (numberFormat is not null)
                ws.Cell(row, 2).Style.NumberFormat.Format = numberFormat;
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 2));
            row++;
        }

        Kpi("Total hours", (double)ReportFormat.Hours(model.Kpis.TotalSeconds), "0.00");
        Kpi("Billable hours", (double)ReportFormat.Hours(model.Kpis.BillableSeconds), "0.00");
        Kpi("Members", model.Kpis.ActiveMembers);
        Kpi("Projects", model.Kpis.ActiveProjects);

        row++;
        ws.Cell(row, 1).Value = "Basis";
        StyleHeaderBand(ws.Range(row, 1, row, 2));
        row++;
        foreach (var line in ReportFormat.WorkloadBasisLines(model.Basis))
        {
            ws.Cell(row, 1).Value = line;
            ws.Range(row, 1, row, 2).Merge();
            StyleMuted(ws.Cell(row, 1));
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 26;
    }

    private static void WriteAllocations(XLWorkbook workbook, WorkloadReportDto model)
    {
        var ws = workbook.Worksheets.Add("Workload");
        ws.TabColor = XLColor.FromHtml(ReportColors.BrandHi);

        ws.Cell(1, 1).Value = "Member";
        ws.Cell(1, 2).Value = "Client";
        ws.Cell(1, 3).Value = "Project";
        ws.Cell(1, 4).Value = "Hours";
        ws.Cell(1, 5).Value = "Billable hours";
        ws.Cell(1, 6).Value = "% of member";
        StyleHeaderBand(ws.Range(1, 1, 1, 6));

        var row = 2;
        foreach (var allocation in model.Allocations)
        {
            ws.Cell(row, 1).Value = allocation.DisplayName;
            ws.Cell(row, 2).Value = allocation.ClientName;
            ws.Cell(row, 3).Value = allocation.ProjectName;
            ws.Cell(row, 4).Value = (double)ReportFormat.Hours(allocation.TotalSeconds);
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 5).Value = (double)ReportFormat.Hours(allocation.BillableSeconds);
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.00";
            // PctOfMemberTotal is on a 0-100 scale; Excel's "%" format multiplies by 100
            // again, so it must be divided down to a 0-1 fraction first (see A5b notes —
            // storing the raw 0-100 value with a "%" format silently renders 100x too big).
            ws.Cell(row, 6).Value = (double)(allocation.PctOfMemberTotal / 100m);
            ws.Cell(row, 6).Style.NumberFormat.Format = "0.0%";
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 6));
            row++;
        }

        if (model.Allocations.Count > 0)
        {
            var lastData = row - 1;
            AddDataBars(ws.Range(2, 4, lastData, 4));

            ws.Cell(row, 1).Value = "Total";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = (double)ReportFormat.Hours(model.GrandTotalSeconds);
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 5).Value = (double)ReportFormat.Hours(model.GrandTotalBillableSeconds);
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml(ReportColors.BrandTint);
        }

        if (model.Allocations.Count > 0)
            ws.Range(1, 1, row - 1, 6).SetAutoFilter();

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        CapColumnWidth(ws, 1, 40);
        CapColumnWidth(ws, 3, 40);
    }

    private static void WriteSchedule(XLWorkbook workbook, WorkloadReportDto model)
    {
        if (model.Schedule.Count == 0) return;

        var ws = workbook.Worksheets.Add("Schedule");
        ws.TabColor = XLColor.FromHtml(ReportColors.PurpleMid);

        ws.Cell(1, 1).Value = "Label";
        ws.Cell(1, 2).Value = "Hours";
        ws.Cell(1, 3).Value = "% of total";
        StyleHeaderBand(ws.Range(1, 1, 1, 3));

        var row = 2;
        foreach (var item in model.Schedule)
        {
            ws.Cell(row, 1).Value = item.Label;
            ws.Cell(row, 2).Value = (double)item.Hours;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            // Same 0-100-scale fix as the Workload sheet above.
            ws.Cell(row, 3).Value = (double)(item.PctOfTotalHours / 100m);
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 3));
            row++;
        }

        var lastRow = row - 1;
        if (lastRow >= 2)
        {
            AddDataBars(ws.Range(2, 2, lastRow, 2));
            ws.Range(1, 1, lastRow, 3).SetAutoFilter();
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void CapColumnWidth(IXLWorksheet ws, int column, double max)
    {
        if (ws.Column(column).Width > max)
            ws.Column(column).Width = max;
    }
}

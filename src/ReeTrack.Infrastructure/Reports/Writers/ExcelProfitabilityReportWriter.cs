using ClosedXML.Excel;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using static ReeTrack.Infrastructure.Reports.Writers.ExcelReportStyles;

namespace ReeTrack.Infrastructure.Reports.Writers;

public sealed class ExcelProfitabilityReportWriter : IReportWriter<ProfitabilityReportDto>
{
    public ReportExportFormat Format => ReportExportFormat.Xlsx;

    public ReportFile Write(ProfitabilityReportDto model)
    {
        using var workbook = new XLWorkbook();
        WriteOverview(workbook, model);
        WriteWeeklyTrend(workbook, model);
        WriteProjects(workbook, model);
        WriteMembers(workbook, model);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportFile(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReportFileNames.ForProfitability(ReportExportFormat.Xlsx, model.GeneratedAtUtc));
    }

    private static void WriteOverview(XLWorkbook workbook, ProfitabilityReportDto model)
    {
        var ws = workbook.Worksheets.Add("Overview");
        ws.TabColor = XLColor.FromHtml(ReportColors.Brand);

        ws.Cell(1, 1).Value = "ReeTrack Profitability Report";
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

        var row = 5;
        ws.Cell(row, 1).Value = "Currency";
        ws.Cell(row, 2).Value = "Revenue";
        ws.Cell(row, 3).Value = "Cost";
        ws.Cell(row, 4).Value = "Margin";
        ws.Cell(row, 5).Value = "Margin %";
        StyleHeaderBand(ws.Range(row, 1, row, 5));
        row++;

        foreach (var currency in model.ByCurrency)
        {
            var money = CurrencyFormat(currency.CurrencyCode);
            ws.Cell(row, 1).Value = currency.CurrencyCode;
            ws.Cell(row, 2).Value = (double)currency.Revenue;
            ws.Cell(row, 2).Style.NumberFormat.Format = money;
            ws.Cell(row, 3).Value = (double)currency.Cost;
            ws.Cell(row, 3).Style.NumberFormat.Format = money;
            ws.Cell(row, 4).Value = (double)currency.Margin;
            ws.Cell(row, 4).Style.NumberFormat.Format = money;
            ws.Cell(row, 4).Style.Font.FontColor =
                XLColor.FromHtml(currency.Margin < 0 ? ReportColors.BrandDeep : ReportColors.Navy);
            if (currency.MarginPct is { } pct)
            {
                ws.Cell(row, 5).Value = (double)(pct / 100m);
                ws.Cell(row, 5).Style.NumberFormat.Format = "0.0%";
            }
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 5));
            row++;
        }

        row += 2;
        ws.Cell(row, 1).Value = "Basis";
        StyleHeaderBand(ws.Range(row, 1, row, 5));
        row++;
        foreach (var line in ReportFormat.ProfitabilityBasisLines(model.Basis))
        {
            ws.Cell(row, 1).Value = line;
            ws.Range(row, 1, row, 5).Merge();
            StyleMuted(ws.Cell(row, 1));
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 26;
    }

    private static void WriteWeeklyTrend(XLWorkbook workbook, ProfitabilityReportDto model)
    {
        var ws = workbook.Worksheets.Add("Weekly trend");
        ws.TabColor = XLColor.FromHtml(ReportColors.Blue);

        ws.Cell(1, 1).Value = "Week start";
        ws.Cell(1, 2).Value = "Currency";
        ws.Cell(1, 3).Value = "Revenue";
        ws.Cell(1, 4).Value = "Cost";
        ws.Cell(1, 5).Value = "Margin";
        StyleHeaderBand(ws.Range(1, 1, 1, 5));

        var row = 2;
        foreach (var week in model.WeeklyTrend)
        {
            var money = CurrencyFormat(week.CurrencyCode);
            ws.Cell(row, 1).Value = week.WeekStartDate.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, 1).Style.NumberFormat.Format = "dd mmm yyyy";
            ws.Cell(row, 2).Value = week.CurrencyCode;
            ws.Cell(row, 3).Value = (double)week.Revenue;
            ws.Cell(row, 3).Style.NumberFormat.Format = money;
            ws.Cell(row, 4).Value = (double)week.Cost;
            ws.Cell(row, 4).Style.NumberFormat.Format = money;
            ws.Cell(row, 5).Value = (double)week.Margin;
            ws.Cell(row, 5).Style.NumberFormat.Format = money;
            ws.Cell(row, 5).Style.Font.FontColor =
                XLColor.FromHtml(week.Margin < 0 ? ReportColors.BrandDeep : ReportColors.Navy);
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 5));
            row++;
        }

        var lastRow = row - 1;
        if (lastRow >= 2)
        {
            AddDataBars(ws.Range(2, 5, lastRow, 5));
            ws.Range(1, 1, lastRow, 5).SetAutoFilter();
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void WriteProjects(XLWorkbook workbook, ProfitabilityReportDto model)
    {
        var ws = workbook.Worksheets.Add("Projects");
        ws.TabColor = XLColor.FromHtml(ReportColors.BrandHi);

        string[] headers =
        [
            "Project", "Client", "Currency", "Billing", "Hours", "Revenue", "Cost", "Margin", "Margin %"
        ];
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        StyleHeaderBand(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var project in model.Projects)
        {
            var money = CurrencyFormat(project.CurrencyCode);
            ws.Cell(row, 1).Value = project.Name;
            ws.Cell(row, 2).Value = project.ClientName;
            ws.Cell(row, 3).Value = project.CurrencyCode;
            ws.Cell(row, 4).Value = project.BillingModel;
            ws.Cell(row, 5).Value = (double)ReportFormat.Hours(project.TotalSeconds);
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 6).Value = (double)project.Revenue;
            ws.Cell(row, 6).Style.NumberFormat.Format = money;
            ws.Cell(row, 7).Value = (double)project.CalculatedCost;
            ws.Cell(row, 7).Style.NumberFormat.Format = money;
            ws.Cell(row, 8).Value = (double)project.Margin;
            ws.Cell(row, 8).Style.NumberFormat.Format = money;
            ws.Cell(row, 8).Style.Font.FontColor =
                XLColor.FromHtml(project.Margin < 0 ? ReportColors.BrandDeep : ReportColors.Navy);
            if (project.MarginPct is { } pct)
            {
                ws.Cell(row, 9).Value = (double)(pct / 100m);
                ws.Cell(row, 9).Style.NumberFormat.Format = "0.0%";
            }
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, headers.Length));
            row++;
        }

        var lastRow = row - 1;
        if (lastRow >= 2)
        {
            AddDataBars(ws.Range(2, 8, lastRow, 8));
            ws.Range(1, 1, lastRow, headers.Length).SetAutoFilter();
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        CapColumnWidth(ws, 1, 40);
        CapColumnWidth(ws, 2, 40);
    }

    private static void WriteMembers(XLWorkbook workbook, ProfitabilityReportDto model)
    {
        var ws = workbook.Worksheets.Add("Members");
        ws.TabColor = XLColor.FromHtml(ReportColors.PurpleMid);

        ws.Cell(1, 1).Value = "Member";
        ws.Cell(1, 2).Value = "Currency";
        ws.Cell(1, 3).Value = "Hours";
        ws.Cell(1, 4).Value = "Labour cost";
        StyleHeaderBand(ws.Range(1, 1, 1, 4));

        var row = 2;
        foreach (var member in model.Members)
        {
            var money = CurrencyFormat(member.CurrencyCode);
            ws.Cell(row, 1).Value = member.DisplayName;
            ws.Cell(row, 2).Value = member.CurrencyCode;
            ws.Cell(row, 3).Value = (double)ReportFormat.Hours(member.TotalSeconds);
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 4).Value = (double)member.LabourCost;
            ws.Cell(row, 4).Style.NumberFormat.Format = money;
            if (row % 2 == 0)
                Zebra(ws.Range(row, 1, row, 4));
            row++;
        }

        var lastRow = row - 1;
        if (lastRow >= 2)
        {
            AddDataBars(ws.Range(2, 4, lastRow, 4));
            ws.Range(1, 1, lastRow, 4).SetAutoFilter();
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        CapColumnWidth(ws, 1, 40);
    }

    private static void CapColumnWidth(IXLWorksheet ws, int column, double max)
    {
        if (ws.Column(column).Width > max)
            ws.Column(column).Width = max;
    }
}

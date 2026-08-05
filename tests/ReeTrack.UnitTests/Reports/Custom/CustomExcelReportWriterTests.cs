using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Infrastructure.Reports.Writers.Custom;
using Xunit;
using Spreadsheet = DocumentFormat.OpenXml.Spreadsheet;

namespace ReeTrack.UnitTests.Reports.Custom;

public class CustomExcelReportWriterTests
{
    [Fact]
    public void Write_GroupedTable_StylesHeaderAndSubtotalRowsDistinctly()
    {
        var model = MinimalReportWithBlocks(
        [
            new TableResult
            {
                Id = "b1",
                Title = "Entries",
                Columns =
                [
                    new TableColumn { Key = "client", Label = "Client", ColumnType = TableColumnType.Text },
                    new TableColumn { Key = "hours", Label = "Hours", ColumnType = TableColumnType.Hours }
                ],
                Rows =
                [
                    new TableRow
                    {
                        Key = "group:0:Acme",
                        Kind = TableRowKind.GroupHeader,
                        Cells = new Dictionary<string, TableCell>
                        {
                            ["client"] = new TableCell { Display = "Acme" },
                            ["hours"] = new TableCell { Display = "" }
                        }
                    },
                    new TableRow
                    {
                        Key = "entry-1",
                        Kind = TableRowKind.Detail,
                        Depth = 1,
                        Cells = new Dictionary<string, TableCell>
                        {
                            ["client"] = new TableCell { Display = "Acme" },
                            ["hours"] = new TableCell { Number = 1m, Display = "1h" }
                        }
                    },
                    new TableRow
                    {
                        Key = "subtotal:0:Acme",
                        Kind = TableRowKind.GroupSubtotal,
                        Cells = new Dictionary<string, TableCell>
                        {
                            ["client"] = new TableCell { Display = "Subtotal — Acme" },
                            ["hours"] = new TableCell { Number = 1m, Display = "1h" }
                        }
                    }
                ]
            }
        ]);

        var file = new CustomExcelReportWriter().Write(model);

        using var stream = new MemoryStream(file.Bytes);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet("Entries");

        // Row 1 is the column header band; data starts at row 2.
        var headerRow = ws.Cell(2, 1);
        var detailRow = ws.Cell(3, 1);
        var subtotalRow = ws.Cell(4, 1);

        Assert.True(headerRow.Style.Font.Bold);
        Assert.True(subtotalRow.Style.Font.Bold);
        Assert.False(detailRow.Style.Font.Bold);

        // The subtotal is shaded; the group header isn't (so the two read as visually distinct).
        Assert.NotEqual(headerRow.Style.Fill.BackgroundColor, subtotalRow.Style.Fill.BackgroundColor);

        // The detail row is indented under its group header.
        Assert.True(detailRow.Style.Alignment.Indent > headerRow.Style.Alignment.Indent);
    }

    [Theory]
    [InlineData(ChartKind.Bar, "barChart", "<c:barDir val=\"col\" />")]
    [InlineData(ChartKind.Line, "lineChart", null)]
    [InlineData(ChartKind.Area, "areaChart", null)]
    [InlineData(ChartKind.Donut, "doughnutChart", "<c:holeSize val=\"55\" />")]
    public void Write_SeriesBlock_InjectsExpectedNativeChartKind(ChartKind kind, string expectedElement, string? expectedFragment)
    {
        var model = MinimalReportWithBlocks(
        [
            SeriesBlock(
                "b1",
                "Weekly hours",
                kind,
                ["Mon", "Tue", "Wed"],
                new NamedSeries { Key = "billable", Label = "Billable", Values = [1m, 2m, 3m] },
                new NamedSeries { Key = "nonBillable", Label = "Non-billable", Values = [4m, 5m, 6m] })
        ]);

        var file = new CustomExcelReportWriter().Write(model);

        using var document = SpreadsheetDocument.Open(new MemoryStream(file.Bytes), false);
        var chartXml = GetChartXml(document, "Weekly hours");

        Assert.NotNull(chartXml);
        Assert.Contains($"<c:{expectedElement}", chartXml);
        if (expectedFragment is not null)
            Assert.Contains(expectedFragment, chartXml);

        // Category axis references column A; the two series reference columns B and C.
        Assert.Contains("'Weekly hours'!$A$2:$A$4", chartXml);
        Assert.Contains("'Weekly hours'!$B$2:$B$4", chartXml);
        Assert.Contains("'Weekly hours'!$C$2:$C$4", chartXml);
        Assert.Contains("'Weekly hours'!$B$1", chartXml); // series name reference
        Assert.Contains("'Weekly hours'!$C$1", chartXml);

        AssertPackageIsSchemaValid(document);
    }

    [Theory]
    [InlineData(0, 1)] // no categories
    [InlineData(1, 0)] // no series
    public void Write_EmptySeriesBlock_SkipsChartInjectionAndStaysValid(int categoryCount, int seriesCount)
    {
        var categories = Enumerable.Range(0, categoryCount).Select(i => $"c{i}").ToArray();
        var series = Enumerable.Range(0, seriesCount)
            .Select(i => new NamedSeries { Key = $"s{i}", Label = $"S{i}", Values = categoryCount > 0 ? [1m] : [] })
            .ToArray();

        var model = MinimalReportWithBlocks(
        [
            SeriesBlock("b1", "Empty series", ChartKind.Bar, categories, series)
        ]);

        var file = new CustomExcelReportWriter().Write(model);

        using var document = SpreadsheetDocument.Open(new MemoryStream(file.Bytes), false);
        var chartXml = GetChartXml(document, "Empty series");

        // Injecting a chart over an empty range produces a workbook Excel reports as
        // corrupt — the injector must skip it outright rather than emit a degenerate chart.
        Assert.Null(chartXml);
        AssertPackageIsSchemaValid(document);

        // Also confirm ClosedXML itself can still reopen the file without throwing.
        using var reload = new XLWorkbook(new MemoryStream(file.Bytes));
        Assert.NotNull(reload.Worksheet("Empty series"));
    }

    [Fact]
    public void Write_SheetTitleWithApostrophe_QuotesSheetNameAndDoublesEmbeddedApostrophe()
    {
        var model = MinimalReportWithBlocks(
        [
            SeriesBlock(
                "b1",
                "Ana's hours",
                ChartKind.Bar,
                ["Mon", "Tue"],
                new NamedSeries { Key = "hours", Label = "Hours", Values = [1m, 2m] })
        ]);

        var file = new CustomExcelReportWriter().Write(model);

        using var document = SpreadsheetDocument.Open(new MemoryStream(file.Bytes), false);
        var chartXml = GetChartXml(document, "Ana's hours");

        Assert.NotNull(chartXml);
        Assert.Contains("'Ana''s hours'!$A$2:$A$3", chartXml);
        Assert.Contains("'Ana''s hours'!$B$2:$B$3", chartXml);

        AssertPackageIsSchemaValid(document);
    }

    [Fact]
    public void Write_SeriesBlock_DoesNotAddDataBarsAnymore()
    {
        var model = MinimalReportWithBlocks(
        [
            SeriesBlock(
                "b1",
                "Weekly hours",
                ChartKind.Bar,
                ["Mon", "Tue"],
                new NamedSeries { Key = "hours", Label = "Hours", Values = [1m, 2m] })
        ]);

        var file = new CustomExcelReportWriter().Write(model);

        using var stream = new MemoryStream(file.Bytes);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet("Weekly hours");

        // AddDataBars was the pre-chart stand-in; a real chart replaces it now.
        Assert.Empty(ws.ConditionalFormats);
    }

    [Fact]
    public void Write_TableWithHoursColumn_StillAddsDataBars()
    {
        var model = MinimalReportWithBlocks(
        [
            new TableResult
            {
                Id = "b1",
                Title = "Entries",
                Columns =
                [
                    new TableColumn { Key = "client", Label = "Client", ColumnType = TableColumnType.Text },
                    new TableColumn { Key = "hours", Label = "Hours", ColumnType = TableColumnType.Hours }
                ],
                Rows =
                [
                    new TableRow
                    {
                        Key = "entry-1",
                        Cells = new Dictionary<string, TableCell>
                        {
                            ["client"] = new TableCell { Display = "Acme" },
                            ["hours"] = new TableCell { Number = 1m, Display = "1h" }
                        }
                    }
                ]
            }
        ]);

        var file = new CustomExcelReportWriter().Write(model);

        using var stream = new MemoryStream(file.Bytes);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet("Entries");

        // WriteTableSheet's AddDataBars call is untouched by this change.
        Assert.NotEmpty(ws.ConditionalFormats);
    }

    private static SeriesResult SeriesBlock(
        string id,
        string title,
        ChartKind kind,
        IReadOnlyList<string> categories,
        params NamedSeries[] series) =>
        new()
        {
            Id = id,
            Title = title,
            Kind = kind,
            Categories = categories,
            Series = series
        };

    private static string? GetChartXml(SpreadsheetDocument document, string sheetName)
    {
        var workbookPart = document.WorkbookPart!;
        var sheetElement = workbookPart.Workbook.Descendants<Spreadsheet.Sheet>()
            .FirstOrDefault(s => s.Name == sheetName);
        Assert.NotNull(sheetElement);

        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheetElement!.Id!.Value!);
        var chartPart = worksheetPart.DrawingsPart?.ChartParts.FirstOrDefault();
        return chartPart?.ChartSpace.OuterXml;
    }

    private static void AssertPackageIsSchemaValid(SpreadsheetDocument document)
    {
        var validator = new OpenXmlValidator();
        var errors = validator.Validate(document).ToList();
        Assert.Empty(errors);
    }

    private static CustomReportDto MinimalReportWithBlocks(IReadOnlyList<ReportBlockResult> blocks) =>
        new()
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = 0,
                BillableSeconds = 0,
                NonBillableSeconds = 0,
                BillablePct = 0m,
                EntryCount = 0,
                ActiveMembers = 0,
                ActiveProjects = 0,
                OvertimeHours = 0,
                WeekendHours = 0,
                HolidayHours = 0,
                UnassignedSeconds = 0
            },
            Basis = new ReportBasisDto
            {
                WeekendPremium = 0.5m,
                HolidayPremium = 1m,
                OvertimePremium = 0.5m,
                WeeklyOvertimeThresholdHours = 40m
            },
            GeneratedAtUtc = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            Blocks = blocks
        };
}

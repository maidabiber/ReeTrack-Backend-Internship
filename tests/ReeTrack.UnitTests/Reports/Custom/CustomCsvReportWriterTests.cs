using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Infrastructure.Reports.Writers.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class CustomCsvReportWriterTests
{
    [Fact]
    public void Write_EmitsSectionPerBlock_WithBom()
    {
        var model = new CustomReportDto
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = 3600,
                BillableSeconds = 3600,
                NonBillableSeconds = 0,
                BillablePct = 100m,
                EntryCount = 1,
                ActiveMembers = 1,
                ActiveProjects = 1,
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
            Blocks =
            [
                new KpiGroupResult
                {
                    Id = "b1",
                    Title = "KPIs",
                    Cells =
                    [
                        new KpiCell
                        {
                            Key = "totalHours",
                            Label = "Total hours",
                            Value = 1m,
                            Unit = MetricUnit.Hours,
                            Display = "1h"
                        }
                    ]
                },
                new ProseResult
                {
                    Id = "b2",
                    Title = "Note",
                    Paragraphs = ["Hello, world"]
                }
            ]
        };

        var file = new CustomCsvReportWriter().Write(model);

        Assert.Equal("text/csv", file.ContentType);
        Assert.StartsWith("reetrack-custom_", file.FileName);
        Assert.Equal(0xEF, file.Bytes[0]); // UTF-8 BOM
        var text = System.Text.Encoding.UTF8.GetString(file.Bytes);
        Assert.Contains("Section,KPIs", text);
        Assert.Contains("Total hours", text);
        Assert.Contains("Hello, world", text);
    }

    [Fact]
    public void Write_GroupedTable_RendersHeaderAndSubtotalRowsPlainly()
    {
        // No writer-specific grouping logic is needed for CSV — the header/subtotal label is
        // already baked into the row's own cell text by the evaluator, so plain row-by-row
        // rendering is enough. This test pins that behaviour down.
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

        var file = new CustomCsvReportWriter().Write(model);
        var text = System.Text.Encoding.UTF8.GetString(file.Bytes);

        Assert.Contains("Acme,", text);
        Assert.Contains("Subtotal — Acme,1", text);
    }

    [Fact]
    public void Write_KpiWithComparison_ShowsPreviousValue()
    {
        var model = MinimalReportWithBlocks(
        [
            new KpiGroupResult
            {
                Id = "b1",
                Title = "KPIs",
                Cells =
                [
                    new KpiCell
                    {
                        Key = "totalHours",
                        Label = "Total hours",
                        Value = 12m,
                        Unit = MetricUnit.Hours,
                        Display = "12h",
                        PreviousValue = 8m,
                        PreviousDisplay = "8h"
                    }
                ]
            }
        ]);

        var file = new CustomCsvReportWriter().Write(model);
        var text = System.Text.Encoding.UTF8.GetString(file.Bytes);

        Assert.Contains("was 8h", text);
    }

    [Fact]
    public void Write_TableCellWithComparison_ShowsPreviousNumber()
    {
        var model = MinimalReportWithBlocks(
        [
            new TableResult
            {
                Id = "b1",
                Title = "By client",
                Columns =
                [
                    new TableColumn { Key = "client", Label = "Client", ColumnType = TableColumnType.Text },
                    new TableColumn { Key = "hours", Label = "Hours", ColumnType = TableColumnType.Hours }
                ],
                Rows =
                [
                    new TableRow
                    {
                        Key = "acme",
                        Cells = new Dictionary<string, TableCell>
                        {
                            ["client"] = new TableCell { Display = "Acme" },
                            ["hours"] = new TableCell { Number = 12m, Display = "12h", PreviousNumber = 8m }
                        }
                    }
                ]
            }
        ]);

        var file = new CustomCsvReportWriter().Write(model);
        var text = System.Text.Encoding.UTF8.GetString(file.Bytes);

        Assert.Contains("was 8", text);
    }

    [Fact]
    public void Write_WithWarnings_ListsThemInTheOverview()
    {
        var model = MinimalReportWithBlocks(
            [new ProseResult { Id = "b1", Paragraphs = ["Note"] }],
            warnings: ["Comparison was skipped: it needs an explicit start and end date on the report filter."]);

        var file = new CustomCsvReportWriter().Write(model);
        var text = System.Text.Encoding.UTF8.GetString(file.Bytes);

        Assert.Contains("Comparison was skipped", text);
    }

    private static CustomReportDto MinimalReportWithBlocks(
        IReadOnlyList<ReportBlockResult> blocks,
        IReadOnlyList<string>? warnings = null) =>
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
            Blocks = blocks,
            Warnings = warnings ?? []
        };
}

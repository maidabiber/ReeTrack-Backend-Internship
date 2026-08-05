using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Infrastructure.Reports.Writers.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class CustomPdfReportWriterTests
{
    [Fact]
    public void Write_GroupedTable_ProducesAValidPdf()
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
            ]
        };

        var file = new CustomPdfReportWriter().Write(model);

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal((byte)'%', file.Bytes[0]);
        Assert.Equal((byte)'P', file.Bytes[1]);
        Assert.Equal((byte)'D', file.Bytes[2]);
        Assert.Equal((byte)'F', file.Bytes[3]);
    }
}

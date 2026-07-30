using System.Text;
using ClosedXML.Excel;
using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Reports.Writers;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class WorkloadReportWriterTests
{
    [Fact]
    public void Csv_Write_IncludesMemberClientProjectHours()
    {
        var model = SampleWorkload();
        var text = Encoding.UTF8.GetString(new CsvWorkloadReportWriter().Write(model).Bytes);

        Assert.Contains("Member,Client,Project,Hours,BillableHours,PctOfMember", text);
        Assert.Contains("Ada", text);
        Assert.Contains("Acme", text);
        Assert.Contains("Alpha", text);
        Assert.Contains("Total,", text);
    }

    [Fact]
    public void Excel_Write_IncludesMemberClientProjectHours_AndGrandTotalRow()
    {
        var model = SampleWorkload();
        var file = new ExcelWorkloadReportWriter().Write(model);

        using var stream = new MemoryStream(file.Bytes);
        using var workbook = new XLWorkbook(stream);
        var workloadSheet = workbook.Worksheet("Workload");

        Assert.Equal("Member", workloadSheet.Cell(1, 1).GetString());
        Assert.Equal("Ada", workloadSheet.Cell(2, 1).GetString());
        Assert.Equal("Acme", workloadSheet.Cell(2, 2).GetString());
        Assert.Equal("Alpha", workloadSheet.Cell(2, 3).GetString());
        Assert.Equal(1d, workloadSheet.Cell(2, 4).GetDouble(), precision: 2); // 3600s -> 1h

        // Grand total row, bolded, one row past the last allocation.
        Assert.Equal("Total", workloadSheet.Cell(4, 1).GetString());
        Assert.True(workloadSheet.Cell(4, 1).Style.Font.Bold);
        Assert.Equal(3d, workloadSheet.Cell(4, 4).GetDouble(), precision: 2); // 10800s -> 3h
    }

    [Fact]
    public void Excel_Write_StoresPercentAsFraction_NotRawZeroToHundredValue()
    {
        // Regression guard: PctOfMemberTotal/PctOfTotalHours are on a 0-100 scale, but
        // Excel's "%" number format multiplies the stored value by 100 again. Storing the
        // raw 33.33 with a "%" format would render "3333.00%" in the opened file — the
        // value must be divided to a 0-1 fraction before the format is applied.
        var model = SampleWorkload();
        var file = new ExcelWorkloadReportWriter().Write(model);

        using var stream = new MemoryStream(file.Bytes);
        using var workbook = new XLWorkbook(stream);
        var workloadSheet = workbook.Worksheet("Workload");

        Assert.Equal(0.3333, workloadSheet.Cell(2, 6).GetDouble(), precision: 4);
        Assert.Equal("0.0%", workloadSheet.Cell(2, 6).Style.NumberFormat.Format);
    }

    [Fact]
    public void Pdf_Write_ReturnsPdfBytes()
    {
        var file = new PdfWorkloadReportWriter().Write(SampleWorkload());

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal((byte)'%', file.Bytes[0]);
        Assert.True(file.Bytes.Length > 100);
    }

    private static WorkloadReportDto SampleWorkload() =>
        new()
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = 10800,
                BillableSeconds = 10800,
                NonBillableSeconds = 0,
                BillablePct = 100,
                EntryCount = 2,
                ActiveMembers = 1,
                ActiveProjects = 2,
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
                WeeklyOvertimeThresholdHours = 40
            },
            GeneratedAtUtc = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc),
            GeneratedByName = "Admin",
            FirstEntryDate = new DateOnly(2026, 7, 20),
            FilterFromDate = new DateOnly(2026, 7, 20),
            FilterToDate = new DateOnly(2026, 7, 26),
            Allocations =
            [
                new WorkloadAllocationDto
                {
                    UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    DisplayName = "Ada",
                    ClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    ClientName = "Acme",
                    ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    ProjectName = "Alpha",
                    TotalSeconds = 3600,
                    BillableSeconds = 3600,
                    PctOfMemberTotal = 33.33m
                },
                new WorkloadAllocationDto
                {
                    UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    DisplayName = "Ada",
                    ClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    ClientName = "Acme",
                    ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    ProjectName = "Beta",
                    TotalSeconds = 7200,
                    BillableSeconds = 7200,
                    PctOfMemberTotal = 66.67m
                }
            ],
            GrandTotalSeconds = 10800,
            GrandTotalBillableSeconds = 10800,
            Schedule =
            [
                new WorkloadScheduleDto { Label = "Overtime", Hours = 0, PctOfTotalHours = 0 }
            ]
        };
}

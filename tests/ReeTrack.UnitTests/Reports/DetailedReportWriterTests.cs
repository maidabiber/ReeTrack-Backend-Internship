using System.Text;
using ClosedXML.Excel;
using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Reports.Writers;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class DetailedReportWriterTests
{
    [Fact]
    public void Csv_Write_WithoutGroups_EmitsFlatEntryRows()
    {
        var model = SampleDetailed(grouped: false);
        var text = Encoding.UTF8.GetString(new CsvDetailedReportWriter().Write(model).Bytes);

        Assert.DoesNotContain("Group,", text);
        Assert.Contains("Alpha", text);
        Assert.Contains("Beta", text);
        Assert.Contains(model.Entries[0].EntryId.ToString(), text);
        Assert.Contains(model.Entries[1].EntryId.ToString(), text);
    }

    [Fact]
    public void Csv_Write_WithGroups_EmitsGroupHeadersBeforeEntryBlocks()
    {
        var model = SampleDetailed(grouped: true);
        var text = Encoding.UTF8.GetString(new CsvDetailedReportWriter().Write(model).Bytes);

        Assert.Contains("Group,Alpha · 1 entries · 1h,", text);
        Assert.Contains("Group,Beta · 1 entries · 2h,", text);

        var alphaHeader = text.IndexOf("Group,Alpha · 1 entries · 1h,", StringComparison.Ordinal);
        var alphaEntry = text.IndexOf(model.Entries[0].EntryId.ToString(), StringComparison.Ordinal);
        var betaHeader = text.IndexOf("Group,Beta · 1 entries · 2h,", StringComparison.Ordinal);
        var betaEntry = text.IndexOf(model.Entries[1].EntryId.ToString(), StringComparison.Ordinal);

        Assert.True(alphaHeader >= 0 && alphaEntry > alphaHeader);
        Assert.True(betaHeader > alphaEntry && betaEntry > betaHeader);
    }

    [Fact]
    public void Csv_Write_NeutralisesFormulaInjectionInUserSuppliedFields()
    {
        // Regression guard: before the CSV writers shared CsvWriterSupport.Escape, only
        // the Summary writer neutralised formula-trigger characters (=+-@) — Detailed,
        // Workload and Profitability quoted commas/quotes but would let a display name
        // like "=cmd|'/c calc'!A1" execute as a formula when the file opens in Excel/Sheets.
        var model = SampleDetailed(grouped: false);
        var maliciousEntry = Entry(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            new DateOnly(2026, 7, 20),
            displayName: "=cmd|'/c calc'!A1",
            clientName: "Acme",
            projectName: "Alpha",
            seconds: 3600,
            cost: 50m);
        model = new DetailedReportDto
        {
            Kpis = model.Kpis,
            Basis = model.Basis,
            GeneratedAtUtc = model.GeneratedAtUtc,
            GeneratedByName = model.GeneratedByName,
            FirstEntryDate = model.FirstEntryDate,
            Entries = [maliciousEntry],
            Page = model.Page,
            PageSize = model.PageSize,
            TotalCount = model.TotalCount,
            Groups = model.Groups
        };

        var text = Encoding.UTF8.GetString(new CsvDetailedReportWriter().Write(model).Bytes);

        Assert.Contains("'=cmd|'/c calc'!A1", text);
    }

    [Fact]
    public void Excel_Write_WithGroups_InsertsMergedGroupRows()
    {
        var model = SampleDetailed(grouped: true);
        var file = new ExcelDetailedReportWriter().Write(model);

        using var stream = new MemoryStream(file.Bytes);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet("Entries");

        Assert.Equal("Alpha · 1 entries · 1h", ws.Cell(2, 1).GetString());
        Assert.True(ws.Cell(2, 1).IsMerged());
        Assert.Equal(ReportFormat.IsoDate(model.Entries[0].EntryDate), ws.Cell(3, 1).GetString());
        Assert.Equal("Alpha", ws.Cell(3, 4).GetString());

        Assert.Equal("Beta · 1 entries · 2h", ws.Cell(4, 1).GetString());
        Assert.True(ws.Cell(4, 1).IsMerged());
        Assert.Equal("Beta", ws.Cell(5, 4).GetString());
    }

    [Fact]
    public void Pdf_Write_WithGroups_ReturnsPdfBytes()
    {
        var model = SampleDetailed(grouped: true);
        var file = new PdfDetailedReportWriter().Write(model);

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal((byte)'%', file.Bytes[0]);
        Assert.Equal((byte)'P', file.Bytes[1]);
        Assert.Equal((byte)'D', file.Bytes[2]);
        Assert.Equal((byte)'F', file.Bytes[3]);
        Assert.True(file.Bytes.Length > 100);
    }

    private static DetailedReportDto SampleDetailed(bool grouped)
    {
        var day = new DateOnly(2026, 7, 20);
        var alphaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var betaId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var entries = new List<DetailedEntryDto>
        {
            Entry(alphaId, day, "Alice", "Acme", "Alpha", 3600, 50m),
            Entry(betaId, day, "Alice", "Acme", "Beta", 7200, 100m)
        };

        IReadOnlyList<DetailedGroupDto> groups = grouped
            ?
            [
                new DetailedGroupDto
                {
                    Label = "Alpha",
                    Keys = ["Alpha"],
                    TotalSeconds = 3600,
                    CalculatedCost = 50m,
                    EntryCount = 1,
                    StartIndex = 0,
                    EndIndexExclusive = 1
                },
                new DetailedGroupDto
                {
                    Label = "Beta",
                    Keys = ["Beta"],
                    TotalSeconds = 7200,
                    CalculatedCost = 100m,
                    EntryCount = 1,
                    StartIndex = 1,
                    EndIndexExclusive = 2
                }
            ]
            : [];

        return new DetailedReportDto
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = 10800,
                BillableSeconds = 10800,
                NonBillableSeconds = 0,
                BillablePct = 100m,
                EntryCount = 2,
                ActiveMembers = 1,
                ActiveProjects = 2,
                OvertimeHours = 0m,
                WeekendHours = 0m,
                HolidayHours = 0m,
                UnassignedSeconds = 0
            },
            Basis = new ReportBasisDto
            {
                WeekendPremium = 0.5m,
                HolidayPremium = 1.0m,
                OvertimePremium = 0.5m,
                WeeklyOvertimeThresholdHours = 40m
            },
            GeneratedAtUtc = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc),
            GeneratedByName = "Admin",
            FirstEntryDate = day,
            Entries = entries,
            Page = 1,
            PageSize = 0,
            TotalCount = 2,
            Groups = groups
        };
    }

    private static DetailedEntryDto Entry(
        Guid entryId,
        DateOnly day,
        string displayName,
        string clientName,
        string projectName,
        long seconds,
        decimal cost) =>
        new()
        {
            EntryId = entryId,
            EntryDate = day,
            StartedAtUtc = day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
            EndedAtUtc = day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc).AddSeconds(seconds),
            UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DisplayName = displayName,
            ClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ClientName = clientName,
            ProjectId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            ProjectName = projectName,
            TaskId = null,
            TaskName = null,
            Tags = [],
            Description = null,
            IsBillable = true,
            DurationSeconds = seconds,
            CurrencyCode = "EUR",
            CalculatedCost = cost,
            NormalCost = cost,
            WeekendCost = 0m,
            HolidayCost = 0m,
            OvertimeCost = 0m,
            OvertimeHours = 0m,
            WeekendHours = 0m,
            HolidayHours = 0m,
            IsWeekend = false,
            IsHoliday = false
        };
}

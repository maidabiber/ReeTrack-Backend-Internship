using System.Text;
using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Reports.Writers;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class CsvReportWriterTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has,comma", "\"has,comma\"")]
    [InlineData("has\"quote", "\"has\"\"quote\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData("line\r\nbreak", "\"line\r\nbreak\"")]
    public void Escape_FollowsRfc4180(string input, string expected)
    {
        Assert.Equal(expected, CsvReportWriter.Escape(input));
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("=cmd|'/c calc'!A1", "'=cmd|'/c calc'!A1")]
    [InlineData("=HYPERLINK(\"http://x\",\"y\")", "\"'=HYPERLINK(\"\"http://x\"\",\"\"y\"\")\"")] // guarded, then RFC-quoted for the quotes
    [InlineData("Normal name", "Normal name")]
    public void Escape_NeutralisesFormulaTriggers(string input, string expected)
    {
        Assert.Equal(expected, CsvReportWriter.Escape(input));
    }

    [Fact]
    public void Write_IncludesEscapedProjectName_AndUtf8Bom()
    {
        var model = SampleSummary(projectName: "Acme, \"Main\"");
        var file = new CsvReportWriter().Write(model);

        Assert.Equal("text/csv", file.ContentType);
        Assert.StartsWith("reetrack-summary_", file.FileName);
        Assert.EndsWith(".csv", file.FileName);

        // UTF-8 BOM
        Assert.Equal(0xEF, file.Bytes[0]);
        Assert.Equal(0xBB, file.Bytes[1]);
        Assert.Equal(0xBF, file.Bytes[2]);

        var text = Encoding.UTF8.GetString(file.Bytes);
        Assert.Contains("\"Acme, \"\"Main\"\"\"", text);
        Assert.Contains("Summary,TotalHours,2h", text);
        // Week starts are ISO so the column sorts and survives a year boundary.
        Assert.Contains("2026-07-20", text);
        // The basis is stated, so the premiums behind any weekend/overtime money are checkable.
        Assert.Contains("Summary,Basis,Confirmed time entries only", text);
    }

    [Fact]
    public void Write_EmitsMoneyAsRawDecimalsWithASeparateCurrencyColumn()
    {
        var text = Encoding.UTF8.GetString(new CsvReportWriter().Write(SampleSummary("Alpha")).Bytes);

        // Cost is a number a spreadsheet can sum, not the "100.00 EUR" label the PDF shows.
        Assert.Contains("Alpha,,,EUR,2h,2,100,,,None,,,100,,100,0,0,0,0,0,0", text);
        Assert.DoesNotContain("100.00 EUR", text);
    }

    [Fact]
    public void Write_EmitsUnassignedRow_SoProjectHoursReconcileToTotal()
    {
        // 3h logged: 2h on Alpha, 1h against no project.
        var sample = SampleSummary(projectName: "Alpha");
        var model = new SummaryReportDto
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = 10800,
                BillableSeconds = 10800,
                NonBillableSeconds = 0,
                BillablePct = 100m,
                EntryCount = 2,
                ActiveMembers = 1,
                ActiveProjects = 1,
                OvertimeHours = 0m,
                WeekendHours = 0m,
                HolidayHours = 0m,
                UnassignedSeconds = 3600
            },
            Activity = sample.Activity,
            WeeklyTrend = sample.WeeklyTrend,
            Projects = sample.Projects,
            Members = sample.Members,
            GeneratedAtUtc = sample.GeneratedAtUtc,
            Basis = new ReportBasisDto
            {
                WeekendPremium = 0.5m,
                HolidayPremium = 1.0m,
                OvertimePremium = 0.5m,
                WeeklyOvertimeThresholdHours = 40m
            }
        };

        var text = Encoding.UTF8.GetString(new CsvReportWriter().Write(model).Bytes);

        Assert.Contains("Summary,UnassignedHours,1h", text);
        // 7200s project + 3600s unassigned = 10800s total → 66.67 + 33.33, and the
        // unassigned row is padded to the full column count.
        Assert.Contains("Unassigned,,,,1h,1,33.33,,,,,,,,,,,,,,", text);
        Assert.Contains(",66.67,", text);
    }

    private static SummaryReportDto SampleSummary(string projectName) =>
        new()
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = 7200,
                BillableSeconds = 7200,
                NonBillableSeconds = 0,
                BillablePct = 100m,
                EntryCount = 1,
                ActiveMembers = 1,
                ActiveProjects = 1,
                OvertimeHours = 0m,
                WeekendHours = 0m,
                HolidayHours = 0m,
                UnassignedSeconds = 0
            },
            Activity = [new DayOfWeekHoursDto { DayOfWeek = "Monday", TotalSeconds = 7200 }],
            WeeklyTrend =
            [
                new TrendPointDto { WeekStartDate = new DateOnly(2026, 7, 20), TotalSeconds = 7200 }
            ],
            Projects =
            [
                new ProjectSummaryDto
                {
                    ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Name = projectName,
                    CurrencyCode = "EUR",
                    TotalSeconds = 7200,
                    CalculatedCost = 100m,
                    NormalCost = 100m,
                    WeekendCost = 0m,
                    HolidayCost = 0m,
                    OvertimeCost = 0m,
                    OvertimeHours = 0m,
                    WeekendHours = 0m,
                    HolidayHours = 0m
                }
            ],
            Members =
            [
                new MemberHoursDto
                {
                    UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    DisplayName = "Ada",
                    TotalSeconds = 7200
                }
            ],
            GeneratedAtUtc = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc),
            Basis = new ReportBasisDto
            {
                WeekendPremium = 0.5m,
                HolidayPremium = 1.0m,
                OvertimePremium = 0.5m,
                WeeklyOvertimeThresholdHours = 40m
            }
        };
}

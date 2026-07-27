using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;
using ReeTrack.Infrastructure.Reports.Writers;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class ReportFormatTests
{
    [Theory]
    [InlineData(0, "0m")]
    [InlineData(45 * 60, "45m")]
    [InlineData(3600, "1h")]
    [InlineData(3600 + 30 * 60, "1h 30m")]
    [InlineData(40 * 3600 + 30 * 60, "40h 30m")]
    public void HoursLabel_FromSeconds(long seconds, string expected) =>
        Assert.Equal(expected, ReportFormat.HoursLabel(seconds));

    [Fact]
    public void Hours_ReturnsDecimalHours() =>
        Assert.Equal(1.5m, ReportFormat.Hours(5400));

    [Theory]
    [InlineData(0, "0%")]
    [InlineData(62.5, "62.5%")]
    [InlineData(100, "100%")]
    public void Percent_Formats(decimal value, string expected) =>
        Assert.Equal(expected, ReportFormat.Percent(value));

    [Fact]
    public void Money_IncludesCurrencyCode_AndNeverOmitsAmount()
    {
        Assert.Equal("1,234.50 EUR", ReportFormat.Money(1234.5m, "eur"));
        Assert.Equal("90.00 USD", ReportFormat.Money(90m, "USD"));
    }

    [Fact]
    public void FriendlyDates_UseInvariantReadableForm()
    {
        Assert.Equal("13 Jul 2026", ReportFormat.FriendlyDate(new DateOnly(2026, 7, 13)));
        Assert.Equal(
            "22 Jul 2026, 12:00 UTC",
            ReportFormat.FriendlyDateTime(new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Highlights_MentionsBusiestDayAndTopProject()
    {
        var model = Sample();
        var text = ReportFormat.Highlights(model);

        Assert.Contains("Team logged 3h across 2 projects (66.67% billable).", text);
        Assert.Contains("Busiest day: Thursday.", text);
        Assert.Contains("Top project: Alpha (66.67% of hours).", text);
        Assert.Contains("Overtime: 1h.", text);
        Assert.Contains("Spend: 150.00 EUR normal, 50.00 EUR weekend", text);
    }

    [Fact]
    public void HighlightLines_AreSeparateFactsAndJoinToHighlights()
    {
        var model = Sample();
        var lines = ReportFormat.HighlightLines(model);

        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.False(string.IsNullOrWhiteSpace(line)));
        Assert.Equal("Team logged 3h across 2 projects (66.67% billable).", lines[0]);
        Assert.Equal(string.Join(' ', lines), ReportFormat.Highlights(model));
    }

    [Fact]
    public void CostByHourType_SplitsSpendPerCurrency()
    {
        var insights = SummaryReportAnalytics.CostByHourType(Sample());

        Assert.Equal(2, insights.Count);
        var eur = Assert.Single(insights, i => i.CurrencyCode == "EUR");
        Assert.Equal(150m, eur.NormalCost);
        Assert.Equal(50m, eur.WeekendCost);
        Assert.Equal(200m, eur.TotalCost);
        Assert.Equal(eur.NormalCost + eur.WeekendCost + eur.HolidayCost + eur.OvertimeCost, eur.TotalCost);
    }

    [Fact]
    public void ScheduleInsights_IncludesTopProjectPerCategory()
    {
        var insights = SummaryReportAnalytics.ScheduleInsights(Sample());

        Assert.Equal(3, insights.Count);
        Assert.Equal("Overtime", insights[0].Label);
        Assert.Equal(1m, insights[0].Hours);
        Assert.Equal("Alpha", insights[0].TopProjectName);
        Assert.Equal("Weekend", insights[1].Label);
        Assert.Equal(0m, insights[1].Hours);
        Assert.Null(insights[1].TopProjectName);
        Assert.Equal("Holiday", insights[2].Label);
    }

    [Fact]
    public void CostByCurrency_GroupsWithoutCrossCurrencySum()
    {
        var insights = SummaryReportAnalytics.CostByCurrency(Sample());

        Assert.Equal(2, insights.Count);
        Assert.Equal("EUR", insights[0].CurrencyCode);
        Assert.Equal(200m, insights[0].TotalCost);
        Assert.Equal("Alpha", insights[0].TopProjectName);
        Assert.Equal("USD", insights[1].CurrencyCode);
        Assert.Equal(90m, insights[1].TotalCost);
        // 200 EUR / 2h = 100 EUR/h
        Assert.Equal(100m, insights[0].AvgCostPerHour);
    }

    [Fact]
    public void PdfAndExcelWriters_EmitNonEmptyMagicBytes()
    {
        var model = Sample();

        var pdf = new PdfReportWriter().Write(model);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.True(pdf.Bytes.Length > 100);
        Assert.Equal("%PDF"u8.ToArray(), pdf.Bytes.Take(4).ToArray());

        var xlsx = new ExcelReportWriter().Write(model);
        Assert.StartsWith("application/vnd.openxmlformats", xlsx.ContentType);
        Assert.True(xlsx.Bytes.Length > 100);
        Assert.Equal([(byte)'P', (byte)'K'], xlsx.Bytes.Take(2).ToArray());
    }

    private static SummaryReportDto Sample() =>
        new()
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = 10800,
                BillableSeconds = 7200,
                NonBillableSeconds = 3600,
                BillablePct = 66.67m,
                EntryCount = 3,
                ActiveMembers = 2,
                ActiveProjects = 2,
                OvertimeHours = 1m,
                WeekendHours = 0m,
                HolidayHours = 0m,
                UnassignedSeconds = 0
            },
            Activity =
            [
                new DayOfWeekHoursDto { DayOfWeek = "Monday", TotalSeconds = 3600 },
                new DayOfWeekHoursDto { DayOfWeek = "Tuesday", TotalSeconds = 0 },
                new DayOfWeekHoursDto { DayOfWeek = "Wednesday", TotalSeconds = 0 },
                new DayOfWeekHoursDto { DayOfWeek = "Thursday", TotalSeconds = 7200 },
                new DayOfWeekHoursDto { DayOfWeek = "Friday", TotalSeconds = 0 },
                new DayOfWeekHoursDto { DayOfWeek = "Saturday", TotalSeconds = 0 },
                new DayOfWeekHoursDto { DayOfWeek = "Sunday", TotalSeconds = 0 }
            ],
            WeeklyTrend =
            [
                new TrendPointDto { WeekStartDate = new DateOnly(2026, 7, 13), TotalSeconds = 3600 },
                new TrendPointDto { WeekStartDate = new DateOnly(2026, 7, 20), TotalSeconds = 7200 }
            ],
            Projects =
            [
                new ProjectSummaryDto
                {
                    ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Name = "Alpha",
                    CurrencyCode = "EUR",
                    TotalSeconds = 7200,
                    CalculatedCost = 200m,
                    NormalCost = 150m,
                    WeekendCost = 50m,
                    HolidayCost = 0m,
                    OvertimeCost = 0m,
                    OvertimeHours = 1m,
                    WeekendHours = 0m,
                    HolidayHours = 0m
                },
                new ProjectSummaryDto
                {
                    ProjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Name = "Beta",
                    CurrencyCode = "USD",
                    TotalSeconds = 3600,
                    CalculatedCost = 90m,
                    NormalCost = 90m,
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
                },
                new MemberHoursDto
                {
                    UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    DisplayName = "Ben",
                    TotalSeconds = 3600
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

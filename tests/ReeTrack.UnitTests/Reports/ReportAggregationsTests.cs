using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Reports;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class ReportAggregationsTests
{
    [Fact]
    public void BuildWeeklyTrend_ZeroFillsMissingWeeks_OldestFirst()
    {
        var currentWeek = new DateOnly(2026, 7, 20); // Monday
        var entries = new[]
        {
            (new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc), 3600L), // current week
            (new DateTime(2026, 7, 7, 10, 0, 0, DateTimeKind.Utc), 7200L)   // two weeks earlier
        };

        var trend = ReportAggregations.BuildWeeklyTrend(entries, currentWeek, weekCount: 4);

        Assert.Equal(4, trend.Count);
        Assert.Equal(new DateOnly(2026, 6, 29), trend[0].WeekStartDate);
        Assert.Equal(0, trend[0].TotalSeconds);
        Assert.Equal(new DateOnly(2026, 7, 6), trend[1].WeekStartDate);
        Assert.Equal(7200, trend[1].TotalSeconds);
        Assert.Equal(new DateOnly(2026, 7, 13), trend[2].WeekStartDate);
        Assert.Equal(0, trend[2].TotalSeconds);
        Assert.Equal(new DateOnly(2026, 7, 20), trend[3].WeekStartDate);
        Assert.Equal(3600, trend[3].TotalSeconds);
    }

    [Fact]
    public void BuildWeeklyTrend_DropsEntriesOutsideWindow()
    {
        var currentWeek = new DateOnly(2026, 7, 20);
        var entries = new[]
        {
            (new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), 9999L), // before window
            (new DateTime(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc), 1111L) // after current week
        };

        var trend = ReportAggregations.BuildWeeklyTrend(entries, currentWeek, weekCount: 2);

        Assert.Equal(2, trend.Count);
        Assert.All(trend, point => Assert.Equal(0, point.TotalSeconds));
    }

    [Fact]
    public void BuildActivity_AlwaysReturnsMondayThroughSunday()
    {
        var activity = ReportAggregations.BuildActivity(
        [
            (DayOfWeek.Wednesday, 1800L),
            (DayOfWeek.Wednesday, 1800L),
            (DayOfWeek.Saturday, 600L)
        ]);

        Assert.Equal(7, activity.Count);
        Assert.Equal(
            ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"],
            activity.Select(d => d.DayOfWeek).ToArray());
        Assert.Equal(0, activity[0].TotalSeconds);
        Assert.Equal(3600, activity[2].TotalSeconds);
        Assert.Equal(600, activity[5].TotalSeconds);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3600, 7200, 50)]
    [InlineData(1, 3, 33.33)]
    public void BillablePct_RoundsAwayFromZero(long billable, long total, decimal expected)
    {
        Assert.Equal(expected, ReportAggregations.BillablePct(billable, total));
    }

    [Fact]
    public void SummaryReportDto_ShapesPortfolioKpisWithoutCostTotal()
    {
        // Hand-built DTO: cost lives only on per-project rows (multi-currency).
        var dto = new SummaryReportDto
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
                WeekendHours = 2m,
                HolidayHours = 0m
            },
            Activity =
            [
                new DayOfWeekHoursDto { DayOfWeek = "Monday", TotalSeconds = 3600 },
                new DayOfWeekHoursDto { DayOfWeek = "Tuesday", TotalSeconds = 0 }
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
                    OvertimeHours = 0m,
                    WeekendHours = 2m,
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
            GeneratedAtUtc = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc)
        };

        Assert.Equal(10800, dto.Kpis.TotalSeconds);
        Assert.Equal(2, dto.Projects.Count);
        Assert.Equal("EUR", dto.Projects[0].CurrencyCode);
        Assert.Equal("USD", dto.Projects[1].CurrencyCode);
        Assert.Equal(290m, dto.Projects.Sum(p => p.CalculatedCost));
        // Portfolio KPIs stay currency-free; cost only appears on project rows.
        Assert.Null(typeof(ReportKpisDto).GetProperty("CalculatedCost"));
    }
}

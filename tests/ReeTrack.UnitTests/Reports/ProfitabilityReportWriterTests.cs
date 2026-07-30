using System.Text;
using ClosedXML.Excel;
using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Reports.Writers;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class ProfitabilityReportWriterTests
{
    private static readonly DateOnly Week1 = new(2026, 7, 6);
    private static readonly DateOnly Week2 = new(2026, 7, 13);

    [Fact]
    public void Csv_Write_KeepsCurrenciesOnSeparateRows()
    {
        var model = SampleProfitability();
        var text = Encoding.UTF8.GetString(new CsvProfitabilityReportWriter().Write(model).Bytes);

        Assert.Contains("EUR,100", text);
        Assert.Contains("USD,200", text);
        // Weekly trend must carry one row per (week, currency) — never a combined row.
        Assert.Contains($"{Week1:yyyy-MM-dd},EUR,100,40,60", text);
        Assert.Contains($"{Week1:yyyy-MM-dd},USD,200,50,150", text);
    }

    [Fact]
    public void Excel_Write_KeepsCurrenciesOnSeparateRows()
    {
        var model = SampleProfitability();
        var file = new ExcelProfitabilityReportWriter().Write(model);

        using var stream = new MemoryStream(file.Bytes);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet("Weekly trend");

        // Two currencies × two weeks -> 4 distinct rows, none combined.
        Assert.Equal("EUR", ws.Cell(2, 2).GetString());
        Assert.Equal(60d, ws.Cell(2, 5).GetDouble());
        Assert.Equal("USD", ws.Cell(3, 2).GetString());
        Assert.Equal(150d, ws.Cell(3, 5).GetDouble());
    }

    [Fact]
    public void Excel_Write_AppliesCurrencyFormatAndAutoFilter_OnDataSheets()
    {
        var model = SampleProfitability();
        var file = new ExcelProfitabilityReportWriter().Write(model);

        using var stream = new MemoryStream(file.Bytes);
        using var workbook = new XLWorkbook(stream);
        var projects = workbook.Worksheet("Projects");

        // Margin % is 0-100 scale on the DTO; must be a 0-1 fraction under a "%" format.
        Assert.Equal(0.60, projects.Cell(2, 9).GetDouble(), precision: 4);
        Assert.Equal("0.0%", projects.Cell(2, 9).Style.NumberFormat.Format);
        Assert.Contains("EUR", projects.Cell(2, 8).Style.NumberFormat.Format);

        Assert.True(projects.AutoFilter.IsEnabled);
    }

    [Fact]
    public void Pdf_Write_ReturnsPdfBytes()
    {
        var file = new PdfProfitabilityReportWriter().Write(SampleProfitability());

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal((byte)'%', file.Bytes[0]);
        Assert.True(file.Bytes.Length > 100);
    }

    [Fact]
    public void GroupWeeklyMarginsByCurrency_DoesNotSumAcrossCurrencies()
    {
        // Regression guard for the bug where the PDF sparkline grouped by week only,
        // silently adding EUR and USD margins into one meaningless figure.
        var trend = new List<WeeklyFinancialTrendDto>
        {
            Trend(Week1, "EUR", revenue: 100m, cost: 40m),
            Trend(Week1, "USD", revenue: 200m, cost: 50m),
            Trend(Week2, "EUR", revenue: 80m, cost: 30m),
            Trend(Week2, "USD", revenue: 120m, cost: 40m)
        };

        var byCurrency = PdfProfitabilityReportWriter.GroupWeeklyMarginsByCurrency(trend);

        Assert.Equal(2, byCurrency.Count);

        var eur = Assert.Single(byCurrency, c => c.CurrencyCode == "EUR");
        Assert.Equal(2, eur.Weeks.Count);
        Assert.Equal(60m, eur.Weeks.Single(w => w.WeekStartDate == Week1).Margin);
        Assert.Equal(50m, eur.Weeks.Single(w => w.WeekStartDate == Week2).Margin);

        var usd = Assert.Single(byCurrency, c => c.CurrencyCode == "USD");
        Assert.Equal(2, usd.Weeks.Count);
        Assert.Equal(150m, usd.Weeks.Single(w => w.WeekStartDate == Week1).Margin);
        Assert.Equal(80m, usd.Weeks.Single(w => w.WeekStartDate == Week2).Margin);
    }

    private static WeeklyFinancialTrendDto Trend(DateOnly week, string currency, decimal revenue, decimal cost) =>
        new()
        {
            WeekStartDate = week,
            CurrencyCode = currency,
            Revenue = revenue,
            Cost = cost,
            Margin = revenue - cost
        };

    private static ProfitabilityReportDto SampleProfitability() =>
        new()
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
            FirstEntryDate = Week1,
            FilterFromDate = Week1,
            FilterToDate = Week2,
            ByCurrency =
            [
                new CurrencyFinancialKpisDto
                {
                    CurrencyCode = "EUR",
                    Revenue = 100m,
                    Cost = 40m,
                    Margin = 60m,
                    MarginPct = 60m,
                    BillableHours = 1m,
                    TotalSeconds = 3600,
                    ProjectCount = 1
                },
                new CurrencyFinancialKpisDto
                {
                    CurrencyCode = "USD",
                    Revenue = 200m,
                    Cost = 50m,
                    Margin = 150m,
                    MarginPct = 75m,
                    BillableHours = 2m,
                    TotalSeconds = 7200,
                    ProjectCount = 1
                }
            ],
            WeeklyTrend =
            [
                Trend(Week1, "EUR", 100m, 40m),
                Trend(Week1, "USD", 200m, 50m),
                Trend(Week2, "EUR", 80m, 30m),
                Trend(Week2, "USD", 120m, 40m)
            ],
            Projects =
            [
                new ProjectProfitabilityDto
                {
                    ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Alpha",
                    CurrencyCode = "EUR",
                    ClientName = "Acme",
                    Status = "Active",
                    BillingModel = "Hourly",
                    HourlyRate = 40m,
                    TotalSeconds = 3600,
                    BillableSeconds = 3600,
                    Revenue = 100m,
                    CalculatedCost = 40m,
                    NormalCost = 40m,
                    WeekendCost = 0m,
                    HolidayCost = 0m,
                    OvertimeCost = 0m,
                    Margin = 60m,
                    MarginPct = 60m
                },
                new ProjectProfitabilityDto
                {
                    ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Beta",
                    CurrencyCode = "USD",
                    ClientName = "Acme",
                    Status = "Active",
                    BillingModel = "Hourly",
                    HourlyRate = 25m,
                    TotalSeconds = 7200,
                    BillableSeconds = 7200,
                    Revenue = 200m,
                    CalculatedCost = 50m,
                    NormalCost = 50m,
                    WeekendCost = 0m,
                    HolidayCost = 0m,
                    OvertimeCost = 0m,
                    Margin = 150m,
                    MarginPct = 75m
                }
            ],
            Members =
            [
                new MemberLabourCostDto
                {
                    UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    DisplayName = "Ada",
                    CurrencyCode = "EUR",
                    TotalSeconds = 3600,
                    LabourCost = 40m
                },
                new MemberLabourCostDto
                {
                    UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    DisplayName = "Ben",
                    CurrencyCode = "USD",
                    TotalSeconds = 7200,
                    LabourCost = 50m
                }
            ],
            RevenueBasisLines =
            [
                "Fixed-fee projects recognize the full fee when the filtered period has any activity (not prorated).",
                "Hourly projects recognize billable hours × project hourly rate.",
                "Margin = revenue − labour cost from ProjectCostCalculator (max of member and project rate).",
                "Amounts are never summed across currencies."
            ]
        };
}

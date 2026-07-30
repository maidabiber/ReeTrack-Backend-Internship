using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Reports;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class ProfitabilityRollupBuilderTests
{
    [Fact]
    public void Build_CostExceedsRevenue_ProducesNegativeMargin()
    {
        var project = ProjectSummary(currencyCode: "EUR", hourlyRate: 10m, calculatedCost: 500m);
        var billableByProject = new Dictionary<Guid, long> { [project.ProjectId] = project.TotalSeconds };

        var (projectRows, byCurrency) = ProfitabilityRollupBuilder.Build([project], billableByProject);

        var row = Assert.Single(projectRows);
        Assert.True(row.Margin < 0m);
        Assert.True(row.MarginPct < 0m);

        var currency = Assert.Single(byCurrency);
        Assert.True(currency.Margin < 0m);
    }

    [Fact]
    public void Build_ZeroRevenue_MarginPctIsUndefinedNotDivideByZero()
    {
        // No hourly rate, no fixed fee -> revenue is 0. MarginPct must be null, not NaN/Infinity.
        var project = ProjectSummary(currencyCode: "EUR", hourlyRate: null, calculatedCost: 0m);
        var billableByProject = new Dictionary<Guid, long>();

        var (projectRows, byCurrency) = ProfitabilityRollupBuilder.Build([project], billableByProject);

        var row = Assert.Single(projectRows);
        Assert.Equal(0m, row.Revenue);
        Assert.Null(row.MarginPct);

        var currency = Assert.Single(byCurrency);
        Assert.Null(currency.MarginPct);
    }

    [Fact]
    public void Build_ProjectWithNoCurrencyCode_GroupsUnderTheSentinelBucket()
    {
        var withCurrency = ProjectSummary(currencyCode: "EUR", hourlyRate: 50m, calculatedCost: 10m);
        var noCurrency = ProjectSummary(currencyCode: "", hourlyRate: 50m, calculatedCost: 10m);
        var billableByProject = new Dictionary<Guid, long>
        {
            [withCurrency.ProjectId] = withCurrency.TotalSeconds,
            [noCurrency.ProjectId] = noCurrency.TotalSeconds
        };

        var (projectRows, byCurrency) = ProfitabilityRollupBuilder.Build(
            [withCurrency, noCurrency], billableByProject);

        Assert.Equal(2, byCurrency.Count);
        Assert.Contains(byCurrency, c => c.CurrencyCode == "EUR");
        Assert.Contains(byCurrency, c => c.CurrencyCode == "—");
        Assert.Contains(projectRows, p => p.CurrencyCode == "—");
    }

    private static ProjectSummaryDto ProjectSummary(string? currencyCode, decimal? hourlyRate, decimal calculatedCost) =>
        new()
        {
            ProjectId = Guid.NewGuid(),
            Name = "Test project",
            CurrencyCode = currencyCode ?? "",
            TotalSeconds = 3600,
            CalculatedCost = calculatedCost,
            NormalCost = calculatedCost,
            WeekendCost = 0m,
            HolidayCost = 0m,
            OvertimeCost = 0m,
            OvertimeHours = 0m,
            WeekendHours = 0m,
            HolidayHours = 0m,
            ClientName = "Acme",
            Status = "Active",
            HourlyRate = hourlyRate,
            FixedFeeAmount = null,
            TimeEstimateHours = null
        };
}

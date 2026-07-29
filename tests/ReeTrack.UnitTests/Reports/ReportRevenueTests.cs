using ReeTrack.Application.Reports;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class ReportRevenueTests
{
    [Fact]
    public void Calculate_FixedFeeWithActivity_RecognizesFullFee()
    {
        var revenue = ReportRevenue.Calculate(
            hourlyRate: 80m,
            fixedFeeAmount: 1000m,
            totalSeconds: 3600,
            billableSeconds: 1800);

        Assert.Equal(1000m, revenue);
    }

    [Fact]
    public void Calculate_FixedFeeWithoutActivity_ReturnsZero()
    {
        var revenue = ReportRevenue.Calculate(
            hourlyRate: null,
            fixedFeeAmount: 1000m,
            totalSeconds: 0,
            billableSeconds: 0);

        Assert.Equal(0m, revenue);
    }

    [Fact]
    public void Calculate_HourlyProject_UsesOnlyBillableSeconds()
    {
        var revenue = ReportRevenue.Calculate(
            hourlyRate: 80m,
            fixedFeeAmount: null,
            totalSeconds: 7200,
            billableSeconds: 5400);

        Assert.Equal(120m, revenue);
    }

    [Fact]
    public void MarginPct_ZeroRevenue_IsUndefined()
    {
        Assert.Null(ReportRevenue.MarginPct(0m, 10m));
    }
}

using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Invoices;
using Xunit;

namespace ReeTrack.UnitTests.Invoices;

public class InvoiceLineBuilderTests
{
    [Fact]
    public void Build_HourlyProject_UsesBillableHoursAndRate()
    {
        var project = Project(
            name: "Website",
            hourlyRate: 80m,
            fixedFee: null,
            totalSeconds: 10 * 3600,
            billableSeconds: 8 * 3600,
            revenue: 640m,
            timeEstimateHours: 6m,
            estimateUsedPct: 166.7m);

        var line = Assert.Single(InvoiceLineBuilder.Build([project]));

        Assert.Equal(InvoiceLineBillingModel.Hourly, line.BillingModel);
        Assert.Equal(8m, line.Quantity);
        Assert.Equal(80m, line.UnitPrice);
        Assert.Equal(640m, line.Amount);
        Assert.Equal("Website · 8h actual · 6h estimated · 166.7% of estimate", line.Description);
    }

    [Fact]
    public void Build_FixedFeeProject_UsesQuantityOneAndIncludesHours()
    {
        var project = Project(
            name: "App",
            hourlyRate: null,
            fixedFee: 2000m,
            totalSeconds: 30 * 3600,
            billableSeconds: 28 * 3600,
            revenue: 2000m,
            timeEstimateHours: 20m,
            estimateUsedPct: 150m);

        var line = Assert.Single(InvoiceLineBuilder.Build([project]));

        Assert.Equal(InvoiceLineBillingModel.FixedFee, line.BillingModel);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(2000m, line.UnitPrice);
        Assert.Equal(2000m, line.Amount);
        Assert.Equal("App · 28h actual · 20h estimated · 150% of estimate", line.Description);
    }

    [Fact]
    public void Build_HourlyProject_WithoutEstimate_OmitsEstimated()
    {
        var project = Project(
            name: "Facebook",
            hourlyRate: 50m,
            fixedFee: null,
            totalSeconds: 11 * 3600,
            billableSeconds: 11 * 3600,
            revenue: 550m,
            timeEstimateHours: null,
            estimateUsedPct: null);

        var line = Assert.Single(InvoiceLineBuilder.Build([project]));

        Assert.Equal("Facebook · 11h actual", line.Description);
    }

    [Fact]
    public void Build_FixedFeeOverEstimate_AddsExtraHourlyLine_WithoutChangingReportRevenue()
    {
        // Report revenue stays at the fixed fee; invoice line builder owns overrun billing.
        var project = Project(
            name: "App",
            hourlyRate: 100m,
            fixedFee: 2000m,
            totalSeconds: 30 * 3600,
            billableSeconds: 28 * 3600,
            revenue: 2000m,
            timeEstimateHours: 20m,
            estimateUsedPct: 150m);

        var lines = InvoiceLineBuilder.Build([project]);
        Assert.Equal(2, lines.Count);

        Assert.Equal(InvoiceLineBillingModel.FixedFee, lines[0].BillingModel);
        Assert.Equal(2000m, lines[0].Amount);
        Assert.Equal("App · Up to 20h estimated", lines[0].Description);

        Assert.Equal(InvoiceLineBillingModel.Hourly, lines[1].BillingModel);
        Assert.Equal(8m, lines[1].Quantity);
        Assert.Equal(100m, lines[1].UnitPrice);
        Assert.Equal(800m, lines[1].Amount);
        Assert.Contains("Extra hours above estimate", lines[1].Description);
    }

    [Fact]
    public void Build_SkipsZeroRevenueAndNoneBilling()
    {
        var none = Project(
            name: "Internal",
            hourlyRate: null,
            fixedFee: null,
            totalSeconds: 3600,
            billableSeconds: 3600,
            revenue: 0m,
            timeEstimateHours: null,
            estimateUsedPct: null);

        Assert.Empty(InvoiceLineBuilder.Build([none]));
    }

    private static ProjectProfitabilityDto Project(
        string name,
        decimal? hourlyRate,
        decimal? fixedFee,
        long totalSeconds,
        long billableSeconds,
        decimal revenue,
        decimal? timeEstimateHours,
        decimal? estimateUsedPct) =>
        new()
        {
            ProjectId = Guid.NewGuid(),
            Name = name,
            CurrencyCode = "EUR",
            ClientName = "Acme",
            Status = "Active",
            BillingModel = fixedFee is > 0 ? "FixedFee" : hourlyRate is > 0 ? "Hourly" : "None",
            HourlyRate = hourlyRate,
            FixedFeeAmount = fixedFee,
            TimeEstimateHours = timeEstimateHours,
            EstimateUsedPct = estimateUsedPct,
            TotalSeconds = totalSeconds,
            BillableSeconds = billableSeconds,
            Revenue = revenue,
            CalculatedCost = 0m,
            NormalCost = 0m,
            WeekendCost = 0m,
            HolidayCost = 0m,
            OvertimeCost = 0m,
            Margin = revenue,
            MarginPct = 100m
        };
}

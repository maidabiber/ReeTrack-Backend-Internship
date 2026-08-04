using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class BlockEvaluatorsCurrencyTests
{
    [Fact]
    public void Breakdown_WithMoneyMetric_SplitsGroupsByCurrency()
    {
        var client = Guid.NewGuid();
        var rows = new[]
        {
            Row(client, "Acme", "EUR", 3600, cost: 50m),
            Row(client, "Acme", "USD", 3600, cost: 80m),
        };

        var result = (TableResult)BlockEvaluators.Evaluate(
            new BreakdownBlockSpec
            {
                Id = "b1",
                Dimensions = ["client"],
                Metrics = ["labourCost"],
                ShowTotals = true
            },
            CustomReportContext.ForTests(rows));

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.Cells["currency"].Display == "EUR");
        Assert.Contains(result.Rows, r => r.Cells["currency"].Display == "USD");
        Assert.Null(result.Totals);
        Assert.Contains("mix currencies", result.Footnote ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Kpi_MoneyMetric_MixedCurrency_OmitsValue()
    {
        var rows = new[]
        {
            Row(Guid.NewGuid(), "A", "EUR", 3600, cost: 50m),
            Row(Guid.NewGuid(), "B", "USD", 3600, cost: 80m),
        };

        var result = (KpiGroupResult)BlockEvaluators.Evaluate(
            new KpiBlockSpec { Id = "b1", Metrics = ["labourCost"] },
            CustomReportContext.ForTests(rows));

        var cell = Assert.Single(result.Cells);
        Assert.Null(cell.Value);
        Assert.Equal("—", cell.Display);
        Assert.Contains("mixes currencies", result.Footnote ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static EntryRow Row(
        Guid clientId,
        string clientLabel,
        string currency,
        long seconds,
        decimal cost)
    {
        var entryId = Guid.NewGuid();
        return new EntryRow(
            entryId,
            Guid.NewGuid(),
            "Ada",
            Guid.NewGuid(),
            "Alpha",
            clientId,
            clientLabel,
            null,
            "(No task)",
            [],
            true,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 6, 29),
            currency,
            seconds,
            null,
            new EntryCostLine(
                entryId, cost, cost, 0, 0, 0,
                seconds / 3600m, 0, 0, 0, false, false));
    }
}

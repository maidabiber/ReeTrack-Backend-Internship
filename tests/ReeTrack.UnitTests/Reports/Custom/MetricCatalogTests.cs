using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class MetricCatalogTests
{
    private static EntryRow Row(
        long seconds,
        bool billable = true,
        EntryCostLine? cost = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ada",
            Guid.NewGuid(),
            "Alpha",
            Guid.NewGuid(),
            "Acme",
            null,
            "(No task)",
            [],
            billable,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 6, 29),
            "EUR",
            seconds,
            null,
            cost);

    [Fact]
    public void TotalHours_SumsDuration()
    {
        var rows = new[] { Row(3600), Row(1800) };
        var input = new MetricInput(rows, null!, 5400);
        var value = MetricCatalog.GetRequired("totalHours").Aggregate(input);
        Assert.Equal(1.5m, value);
    }

    [Fact]
    public void BillablePct_UsesBillableShare()
    {
        var rows = new[] { Row(3600, billable: true), Row(3600, billable: false) };
        var input = new MetricInput(rows, null!, 7200);
        var value = MetricCatalog.GetRequired("billablePct").Aggregate(input);
        Assert.Equal(50m, value);
    }

    [Fact]
    public void EntryCount_CountsRows()
    {
        var rows = new[] { Row(3600), Row(7200), Row(900) };
        var input = new MetricInput(rows, null!, 11700);
        Assert.Equal(3m, MetricCatalog.GetRequired("entryCount").Aggregate(input));
    }
}

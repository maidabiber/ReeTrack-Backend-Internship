using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class EntriesGroupingTests
{
    [Fact]
    public void NoGroupBy_RendersFlatDetailRowsOnly()
    {
        var rows = new[]
        {
            Row("Acme", "Website", "USD", 3600, 100m),
            Row("Acme", "Website", "USD", 3600, 100m),
        };

        var result = Evaluate(rows, groupBy: []);

        Assert.All(result.Rows, r => Assert.Equal(TableRowKind.Detail, r.Kind));
        Assert.Equal(2, result.Rows.Count);
        Assert.Null(result.Footnote);
    }

    [Fact]
    public void GroupByClient_EmitsHeaderDetailSubtotalInOrder()
    {
        var rows = new[]
        {
            Row("Acme", "Website", "USD", 3600, 100m),
            Row("Acme", "Website", "USD", 7200, 200m),
            Row("Globex", "Rocket", "USD", 3600, 50m),
        };

        var result = Evaluate(rows, groupBy: [ReportGroupBy.Client]);

        Assert.Equal(
            [TableRowKind.GroupHeader, TableRowKind.Detail, TableRowKind.Detail, TableRowKind.GroupSubtotal,
             TableRowKind.GroupHeader, TableRowKind.Detail, TableRowKind.GroupSubtotal],
            result.Rows.Select(r => r.Kind).ToList());

        var acmeSubtotal = result.Rows[3];
        Assert.Equal(3m, acmeSubtotal.Cells["hours"].Number);
        Assert.Equal(300m, acmeSubtotal.Cells["labourCost"].Number);
        Assert.Contains("USD", acmeSubtotal.Cells["labourCost"].Display, StringComparison.Ordinal);

        var globexSubtotal = result.Rows[6];
        Assert.Equal(1m, globexSubtotal.Cells["hours"].Number);
        Assert.Equal(50m, globexSubtotal.Cells["labourCost"].Number);
    }

    [Fact]
    public void MultiLevelGroupBy_NestsHeadersAndSubtotalsByDepth()
    {
        // "Alpha" sorts before "Beta", so the two-row group stays first — keeps the expected
        // row sequence below readable instead of depending on alphabetical tie-breaking.
        var rows = new[]
        {
            Row("Acme", "Alpha", "USD", 3600, 100m),
            Row("Acme", "Alpha", "USD", 3600, 100m),
            Row("Acme", "Beta", "USD", 3600, 100m),
        };

        var result = Evaluate(rows, groupBy: [ReportGroupBy.Client, ReportGroupBy.Project]);

        // client header (depth 0) -> project header (depth 1) -> 2 detail (depth 2) ->
        // project subtotal (depth 1) -> project header (depth 1) -> detail (depth 2) ->
        // project subtotal (depth 1) -> client subtotal (depth 0)
        var kinds = result.Rows.Select(r => (r.Kind, r.Depth)).ToList();
        Assert.Equal((TableRowKind.GroupHeader, 0), kinds[0]);
        Assert.Equal((TableRowKind.GroupHeader, 1), kinds[1]);
        Assert.Equal((TableRowKind.Detail, 2), kinds[2]);
        Assert.Equal((TableRowKind.Detail, 2), kinds[3]);
        Assert.Equal((TableRowKind.GroupSubtotal, 1), kinds[4]);
        Assert.Equal((TableRowKind.GroupHeader, 1), kinds[5]);
        Assert.Equal((TableRowKind.Detail, 2), kinds[6]);
        Assert.Equal((TableRowKind.GroupSubtotal, 1), kinds[7]);
        Assert.Equal((TableRowKind.GroupSubtotal, 0), kinds[8]);

        var clientSubtotal = result.Rows[8];
        Assert.Equal(3m, clientSubtotal.Cells["hours"].Number);
        Assert.Equal(300m, clientSubtotal.Cells["labourCost"].Number);
    }

    [Fact]
    public void MixedCurrencyInGroup_BlanksMoneySubtotalButKeepsHours()
    {
        var rows = new[]
        {
            Row("Acme", "Website", "USD", 3600, 100m),
            Row("Acme", "Website", "EUR", 3600, 90m),
        };

        var result = Evaluate(rows, groupBy: [ReportGroupBy.Client]);

        var subtotal = Assert.Single(result.Rows, r => r.Kind == TableRowKind.GroupSubtotal);
        Assert.Equal("—", subtotal.Cells["labourCost"].Display);
        Assert.Null(subtotal.Cells["labourCost"].Number);
        Assert.Equal(2m, subtotal.Cells["hours"].Number);
    }

    [Fact]
    public void NoCostData_SubtotalShowsDashNotZero()
    {
        var rows = new[]
        {
            Row("Acme", "Website", "USD", 3600, cost: null),
            Row("Acme", "Website", "USD", 3600, cost: null),
        };

        var result = Evaluate(rows, groupBy: [ReportGroupBy.Client]);

        var subtotal = Assert.Single(result.Rows, r => r.Kind == TableRowKind.GroupSubtotal);
        Assert.Equal("—", subtotal.Cells["labourCost"].Display);
        Assert.Equal(2m, subtotal.Cells["hours"].Number);
    }

    [Fact]
    public void RowLimitInsideGroup_AddsPartialFootnoteAndPartialSubtotal()
    {
        var rows = new[]
        {
            Row("Acme", "Website", "USD", 3600, 100m),
            Row("Acme", "Website", "USD", 3600, 100m),
            Row("Acme", "Website", "USD", 3600, 100m),
        };

        var result = Evaluate(rows, groupBy: [ReportGroupBy.Client], limit: 2);

        var subtotal = Assert.Single(result.Rows, r => r.Kind == TableRowKind.GroupSubtotal);
        Assert.Equal(2m, subtotal.Cells["hours"].Number);
        Assert.Contains("row limit", result.Footnote ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RowLimitAtGroupBoundary_NoPartialFootnote()
    {
        var rows = new[]
        {
            Row("Acme", "Website", "USD", 3600, 100m),
            Row("Acme", "Website", "USD", 3600, 100m),
            Row("Globex", "Rocket", "USD", 3600, 50m),
        };

        // Limit lands exactly at the boundary between Acme and Globex — Acme's subtotal is complete.
        var result = Evaluate(rows, groupBy: [ReportGroupBy.Client], limit: 2);

        Assert.Null(result.Footnote);
        Assert.DoesNotContain(result.Rows, r => r.Cells.TryGetValue("client", out var c) && c.Display == "Globex");
    }

    private static TableResult Evaluate(
        IReadOnlyList<EntryRow> rows,
        IReadOnlyList<ReportGroupBy> groupBy,
        int limit = 100)
    {
        return (TableResult)BlockEvaluators.Evaluate(
            new EntriesBlockSpec
            {
                Id = "entries1",
                Columns = ["client", "hours", "labourCost"],
                GroupBy = groupBy,
                Limit = limit
            },
            CustomReportContext.ForTests(rows));
    }

    private static EntryRow Row(
        string clientLabel,
        string projectLabel,
        string currency,
        long seconds,
        decimal? cost)
    {
        var entryId = Guid.NewGuid();
        return new EntryRow(
            entryId,
            Guid.NewGuid(),
            "Ada",
            Guid.NewGuid(),
            projectLabel,
            Guid.NewGuid(),
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
            cost is null
                ? null
                : new EntryCostLine(
                    entryId, cost.Value, cost.Value, 0, 0, 0,
                    seconds / 3600m, 0, 0, 0, false, false));
    }
}

using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Infrastructure.Reports.Custom.Insights;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

/// <summary>
/// Reference resolution is what keeps invented figures off the page: the model names a block,
/// row, and column, and anything that does not resolve is dropped rather than printed.
/// </summary>
public class InsightFactsTests
{
    // The insight digest reads block content, not these headline figures.
    private static readonly ReportKpisDto EmptyKpis = new()
    {
        TotalSeconds = 0,
        BillableSeconds = 0,
        NonBillableSeconds = 0,
        BillablePct = 0m,
        EntryCount = 0,
        ActiveMembers = 0,
        ActiveProjects = 0,
        OvertimeHours = 0m,
        WeekendHours = 0m,
        HolidayHours = 0m,
        UnassignedSeconds = 0
    };

    private static readonly ReportBasisDto EmptyBasis = new()
    {
        WeekendPremium = 1m,
        HolidayPremium = 1m,
        OvertimePremium = 1m,
        WeeklyOvertimeThresholdHours = 40m
    };

    private static CustomReportDto Report(params ReportBlockResult[] blocks) =>
        new()
        {
            Kpis = EmptyKpis,
            Basis = EmptyBasis,
            GeneratedAtUtc = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            FilterFromDate = new DateOnly(2026, 7, 1),
            FilterToDate = new DateOnly(2026, 7, 31),
            Blocks = blocks
        };

    private static TableResult ClientTable() =>
        new()
        {
            Id = "b2",
            Title = "By client",
            Columns =
            [
                new TableColumn { Key = "client", Label = "Client", ColumnType = TableColumnType.Text },
                new TableColumn { Key = "totalHours", Label = "Total hours", ColumnType = TableColumnType.Hours }
            ],
            Rows =
            [
                new TableRow
                {
                    Key = "acme",
                    Cells = new Dictionary<string, TableCell>
                    {
                        ["client"] = new() { Display = "Acme" },
                        ["totalHours"] = new() { Number = 120m, Display = "120.00h", PreviousNumber = 90m }
                    }
                }
            ]
        };

    private static KpiGroupResult Kpis() =>
        new()
        {
            Id = "b1",
            Title = "KPIs",
            Cells =
            [
                new KpiCell
                {
                    Key = "totalHours",
                    Label = "Total hours",
                    Value = 200m,
                    Unit = MetricUnit.Hours,
                    Display = "200.00h",
                    PreviousValue = 180m,
                    PreviousDisplay = "180.00h"
                }
            ]
        };

    [Fact]
    public void ResolvesAKpiReferenceWithItsBaseline()
    {
        var facts = InsightFacts.From(Report(Kpis()));

        Assert.Equal("Total hours: 200.00h (was 180.00h)", facts.ResolveReference("b1", null, "totalHours"));
    }

    [Fact]
    public void ResolvesATableCellUsingTheRowsOwnLabel()
    {
        var facts = InsightFacts.From(Report(ClientTable()));

        Assert.Equal("Acme — Total hours: 120.00h, was 90", facts.ResolveReference("b2", "acme", "totalHours"));
    }

    [Theory]
    [InlineData("nope", "acme", "totalHours")]   // block that is not in the report
    [InlineData("b2", "globex", "totalHours")]   // client that was never listed
    [InlineData("b2", "acme", "margin")]         // metric the block does not carry
    [InlineData("b2", null, "totalHours")]       // table cell with no row named
    public void DoesNotResolveAnythingTheReportDoesNotContain(string? blockId, string? rowKey, string? columnKey)
    {
        var facts = InsightFacts.From(Report(Kpis(), ClientTable()));

        Assert.Null(facts.ResolveReference(blockId, rowKey, columnKey));
    }

    [Fact]
    public void DigestCarriesTheKeysTheModelMustQuoteBack()
    {
        var facts = InsightFacts.From(Report(Kpis(), ClientTable()));

        Assert.Contains("BLOCK id=b2", facts.Digest, StringComparison.Ordinal);
        Assert.Contains("row=acme", facts.Digest, StringComparison.Ordinal);
        Assert.Contains("totalHours (Total hours)", facts.Digest, StringComparison.Ordinal);
    }

    [Fact]
    public void DigestLeavesExistingProseOut()
    {
        // Otherwise a regenerate would let the model build on its own earlier commentary.
        var facts = InsightFacts.From(Report(new ProseResult
        {
            Id = "b3",
            Title = "AI insights",
            Paragraphs = ["Acme grew a lot last month."]
        }));

        Assert.DoesNotContain("Acme grew", facts.Digest, StringComparison.Ordinal);
    }

    [Fact]
    public void HasComparisonFollowsTheReport()
    {
        Assert.False(InsightFacts.From(Report(Kpis())).HasComparison);
    }
}

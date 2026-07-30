using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Reports;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class DetailedReportGroupingTests
{
    [Fact]
    public void BuildGroups_GroupsContiguousEntriesByProject_WithCorrectTotals()
    {
        var day = new DateOnly(2026, 7, 20);
        var entries = new List<DetailedEntryDto>
        {
            Entry("Alpha", day, seconds: 3600, cost: 50m),
            Entry("Alpha", day, seconds: 1800, cost: 25m),
            Entry("Beta", day, seconds: 7200, cost: 100m)
        };
        var groupBy = new List<ReportGroupBy> { ReportGroupBy.Project };

        var sorted = DetailedReportGrouping.Sort(entries, groupBy);
        var groups = DetailedReportGrouping.BuildGroups(sorted, groupBy);

        Assert.Equal(2, groups.Count);

        var alpha = Assert.Single(groups, g => g.Label == "Alpha");
        Assert.Equal(2, alpha.EntryCount);
        Assert.Equal(5400, alpha.TotalSeconds);
        Assert.Equal(75m, alpha.CalculatedCost);
        Assert.Equal(alpha.EndIndexExclusive - alpha.StartIndex, alpha.EntryCount);

        var beta = Assert.Single(groups, g => g.Label == "Beta");
        Assert.Equal(1, beta.EntryCount);
        Assert.Equal(7200, beta.TotalSeconds);
        Assert.Equal(100m, beta.CalculatedCost);

        // Index ranges must partition the sorted list with no gap or overlap.
        var orderedByStart = groups.OrderBy(g => g.StartIndex).ToList();
        Assert.Equal(0, orderedByStart[0].StartIndex);
        Assert.Equal(orderedByStart[0].EndIndexExclusive, orderedByStart[1].StartIndex);
        Assert.Equal(sorted.Count, orderedByStart[^1].EndIndexExclusive);
    }

    [Fact]
    public void BuildGroups_WithManySmallGroups_PartitionsEveryEntryExactlyOnce()
    {
        // Exercises the two-pointer scan across many group boundaries — the regression
        // case for the old Skip/Take-based O(n²) implementation.
        var day = new DateOnly(2026, 7, 20);
        var entries = Enumerable.Range(0, 50)
            .Select(i => Entry($"Project{i}", day, seconds: 60, cost: 1m))
            .ToList();
        var groupBy = new List<ReportGroupBy> { ReportGroupBy.Project };

        var sorted = DetailedReportGrouping.Sort(entries, groupBy);
        var groups = DetailedReportGrouping.BuildGroups(sorted, groupBy);

        Assert.Equal(50, groups.Count);
        Assert.All(groups, g => Assert.Equal(1, g.EntryCount));
        Assert.Equal(50, groups.Sum(g => g.EntryCount));
    }

    [Fact]
    public void BuildGroups_NoGroupBy_ReturnsEmpty()
    {
        var entries = new List<DetailedEntryDto> { Entry("Alpha", new DateOnly(2026, 7, 20), 3600, 50m) };

        var groups = DetailedReportGrouping.BuildGroups(entries, []);

        Assert.Empty(groups);
    }

    private static DetailedEntryDto Entry(string projectName, DateOnly day, long seconds, decimal cost) =>
        new()
        {
            EntryId = Guid.NewGuid(),
            EntryDate = day,
            StartedAtUtc = day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
            UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DisplayName = "Ada",
            ProjectName = projectName,
            Tags = [],
            IsBillable = true,
            DurationSeconds = seconds,
            CalculatedCost = cost,
            NormalCost = cost,
            WeekendCost = 0m,
            HolidayCost = 0m,
            OvertimeCost = 0m,
            OvertimeHours = 0m,
            WeekendHours = 0m,
            HolidayHours = 0m,
            IsWeekend = false,
            IsHoliday = false
        };
}

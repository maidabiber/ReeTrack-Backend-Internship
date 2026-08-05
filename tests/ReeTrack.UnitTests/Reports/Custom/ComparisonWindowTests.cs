using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class ComparisonWindowTests
{
    private static ReportQuery Range(DateOnly? from, DateOnly? to) =>
        new() { From = from, To = to, ProjectIds = [Guid.Parse("11111111-1111-1111-1111-111111111111")] };

    [Fact]
    public void PreviousPeriod_IsTheEqualLengthWindowEndingTheDayBefore()
    {
        var query = Range(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.True(ComparisonWindow.TryResolve(query, ComparisonMode.PreviousPeriod, out var baseline));

        // July is 31 days, so the baseline is the 31 days ending 30 June.
        Assert.Equal(new DateOnly(2026, 5, 31), baseline.From);
        Assert.Equal(new DateOnly(2026, 6, 30), baseline.To);
    }

    [Fact]
    public void PreviousPeriod_HandlesASingleDay()
    {
        var query = Range(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1));

        Assert.True(ComparisonWindow.TryResolve(query, ComparisonMode.PreviousPeriod, out var baseline));

        Assert.Equal(new DateOnly(2026, 6, 30), baseline.From);
        Assert.Equal(new DateOnly(2026, 6, 30), baseline.To);
    }

    [Fact]
    public void SamePeriodLastYear_ShiftsBothEnds()
    {
        var query = Range(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.True(ComparisonWindow.TryResolve(query, ComparisonMode.SamePeriodLastYear, out var baseline));

        Assert.Equal(new DateOnly(2025, 7, 1), baseline.From);
        Assert.Equal(new DateOnly(2025, 7, 31), baseline.To);
    }

    [Fact]
    public void SamePeriodLastYear_ClampsALeapDay()
    {
        var query = Range(new DateOnly(2024, 2, 29), new DateOnly(2024, 2, 29));

        Assert.True(ComparisonWindow.TryResolve(query, ComparisonMode.SamePeriodLastYear, out var baseline));

        Assert.Equal(new DateOnly(2023, 2, 28), baseline.From);
    }

    [Fact]
    public void EveryOtherFilterIsCarriedOver()
    {
        var query = Range(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        ComparisonWindow.TryResolve(query, ComparisonMode.PreviousPeriod, out var baseline);

        // The two windows must differ by date alone, or the comparison is not like for like.
        Assert.Equal(query.ProjectIds, baseline.ProjectIds);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-07-01", null)]
    [InlineData(null, "2026-07-31")]
    public void AnOpenEndedRangeHasNoBaseline(string? from, string? to)
    {
        // An open range has no defined length; inventing one would compare against a window
        // the user never asked for.
        var query = Range(
            from is null ? null : DateOnly.Parse(from),
            to is null ? null : DateOnly.Parse(to));

        Assert.False(ComparisonWindow.TryResolve(query, ComparisonMode.PreviousPeriod, out _));
    }

    [Fact]
    public void NoneResolvesToNothing()
    {
        var query = Range(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.False(ComparisonWindow.TryResolve(query, ComparisonMode.None, out _));
    }
}

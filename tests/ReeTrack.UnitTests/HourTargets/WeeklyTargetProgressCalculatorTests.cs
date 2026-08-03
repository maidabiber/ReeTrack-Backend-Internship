using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;
using Xunit;

namespace ReeTrack.UnitTests.HourTargets;

public class WeeklyTargetProgressCalculatorTests
{
    [Fact]
    public void DailyMode_MultipliesByFullWeekWorkdays()
    {
        // Week of Mon 27 Jul 2026; no holidays → 5 workdays → 40h target
        var weekStart = new DateOnly(2026, 7, 27);
        var friday = new DateOnly(2026, 7, 31);
        var logged = new Dictionary<DateOnly, int>
        {
            [weekStart] = 8 * 3600,
            [weekStart.AddDays(1)] = 8 * 3600,
            [weekStart.AddDays(2)] = 6 * 3600,
        };

        var progress = WeeklyTargetProgressCalculator.Calculate(
            HourTargetMode.Daily,
            8m,
            logged,
            weekStart,
            friday,
            new HashSet<DateOnly>());

        Assert.Equal(40m, progress.TargetHours);
        Assert.Equal(22m, progress.LoggedHours);
        Assert.Equal(18m, progress.RemainingHours);
        Assert.False(progress.OnTrack);
        Assert.Equal(weekStart.AddDays(3), progress.WeakestDay); // Thursday with 0h
        Assert.Equal(0m, progress.WeakestDayHours);
    }

    [Fact]
    public void WeeklyMode_UsesFixedTarget_AndWeakestIncludesZeroDays()
    {
        var weekStart = new DateOnly(2026, 7, 27);
        var friday = new DateOnly(2026, 7, 31);
        var logged = new Dictionary<DateOnly, int>
        {
            [weekStart] = 10 * 3600,
        };

        var progress = WeeklyTargetProgressCalculator.Calculate(
            HourTargetMode.Weekly,
            40m,
            logged,
            weekStart,
            friday,
            new HashSet<DateOnly>());

        Assert.Equal(40m, progress.TargetHours);
        Assert.Equal(10m, progress.LoggedHours);
        Assert.Equal(weekStart.AddDays(1), progress.WeakestDay); // Tuesday with 0h
        Assert.Equal(0m, progress.WeakestDayHours);
    }

    [Fact]
    public void OnTrack_WhenLoggedMeetsTarget()
    {
        var weekStart = new DateOnly(2026, 7, 27);
        var friday = new DateOnly(2026, 7, 31);
        var logged = new Dictionary<DateOnly, int>
        {
            [weekStart] = 40 * 3600,
        };

        var progress = WeeklyTargetProgressCalculator.Calculate(
            HourTargetMode.Weekly,
            40m,
            logged,
            weekStart,
            friday,
            new HashSet<DateOnly>());

        Assert.True(progress.OnTrack);
        Assert.Equal(0m, progress.RemainingHours);
    }
}

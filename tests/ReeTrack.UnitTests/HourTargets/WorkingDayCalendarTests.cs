using ReeTrack.Domain.Services;
using Xunit;

namespace ReeTrack.UnitTests.HourTargets;

public class WorkingDayCalendarTests
{
    [Fact]
    public void IsWorkday_ExcludesWeekendsAndHolidays()
    {
        var monday = new DateOnly(2026, 7, 27);
        var saturday = new DateOnly(2026, 8, 1);
        var holiday = new DateOnly(2026, 7, 28);
        var holidays = new HashSet<DateOnly> { holiday };

        Assert.True(WorkingDayCalendar.IsWorkday(monday, holidays));
        Assert.False(WorkingDayCalendar.IsWorkday(saturday, holidays));
        Assert.False(WorkingDayCalendar.IsWorkday(holiday, holidays));
    }

    [Fact]
    public void CountWorkdaysInRange_SkipsWeekendAndHoliday()
    {
        // Mon Jul 27 – Sun Aug 2, 2026 with Tue as holiday → 4 workdays
        var start = new DateOnly(2026, 7, 27);
        var end = new DateOnly(2026, 8, 2);
        var holidays = new HashSet<DateOnly> { new(2026, 7, 28) };

        Assert.Equal(4, WorkingDayCalendar.CountWorkdaysInRange(start, end, holidays));
    }
}

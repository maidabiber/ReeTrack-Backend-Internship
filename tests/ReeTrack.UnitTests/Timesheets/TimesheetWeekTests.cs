using ReeTrack.Infrastructure.Timesheets;
using Xunit;

namespace ReeTrack.UnitTests.Timesheets;

public class TimesheetWeekTests
{
    [Theory]
    // Monday midnight maps to itself.
    [InlineData("2026-07-13T00:00:00", "2026-07-13")]
    // Mid-week noon (DurationOnly anchor) maps back to Monday.
    [InlineData("2026-07-15T12:00:00", "2026-07-13")]
    // Sunday belongs to the week started the previous Monday.
    [InlineData("2026-07-19T23:59:59", "2026-07-13")]
    // Next Monday starts a new week.
    [InlineData("2026-07-20T00:00:00", "2026-07-20")]
    // Year boundary: Thursday 2026-01-01 belongs to Monday 2025-12-29.
    [InlineData("2026-01-01T08:00:00", "2025-12-29")]
    public void ToWeekStart_ReturnsUtcMonday(string instant, string expectedMonday)
    {
        var utc = DateTime.SpecifyKind(DateTime.Parse(instant), DateTimeKind.Utc);

        var weekStart = TimesheetWeek.ToWeekStart(utc);

        Assert.Equal(DateOnly.Parse(expectedMonday), weekStart);
        Assert.Equal(DayOfWeek.Monday, weekStart.DayOfWeek);
    }
}

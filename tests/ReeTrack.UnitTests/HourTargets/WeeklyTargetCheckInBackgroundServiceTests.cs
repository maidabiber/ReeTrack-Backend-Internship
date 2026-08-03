using ReeTrack.Application.Common.Options;
using ReeTrack.Infrastructure.Background;
using Xunit;

namespace ReeTrack.UnitTests.HourTargets;

public class WeeklyTargetCheckInBackgroundServiceTests
{
    [Fact]
    public void IsInSendWindow_MatchesConfiguredFridayNoon()
    {
        // 2026-07-31 10:00 UTC = 12:00 in Europe/Zagreb (CEST, UTC+2)
        var utc = new DateTime(2026, 7, 31, 10, 0, 30, DateTimeKind.Utc);
        var options = new WeeklyTargetCheckInOptions
        {
            TimeZone = "Europe/Zagreb",
            DayOfWeek = DayOfWeek.Friday,
            AtLocalTime = "12:00"
        };

        Assert.True(WeeklyTargetCheckInBackgroundService.IsInSendWindow(utc, options));
    }

    [Fact]
    public void IsInSendWindow_RejectsWrongMinute()
    {
        var utc = new DateTime(2026, 7, 31, 10, 1, 0, DateTimeKind.Utc);
        var options = new WeeklyTargetCheckInOptions
        {
            TimeZone = "Europe/Zagreb",
            DayOfWeek = DayOfWeek.Friday,
            AtLocalTime = "12:00"
        };

        Assert.False(WeeklyTargetCheckInBackgroundService.IsInSendWindow(utc, options));
    }
}

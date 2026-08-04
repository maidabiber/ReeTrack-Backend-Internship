using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

/// <summary>
/// The cost line's hour buckets overlap, so hourType fans out. Forcing one bucket per entry
/// made a Saturday-overtime shift count wholly as "Weekend" while the overtimeHours metric,
/// read from the same cost line, still reported overtime.
/// </summary>
public class HourTypeDimensionTests
{
    private static EntryRow Row(DateOnly date, EntryCostLine? cost) =>
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
            true,
            date,
            new DateOnly(2026, 6, 29),
            "EUR",
            28800,
            null,
            cost);

    private static EntryCostLine Cost(
        decimal totalHours,
        decimal weekendHours = 0m,
        decimal holidayHours = 0m,
        decimal overtimeHours = 0m,
        bool isWeekend = false,
        bool isHoliday = false) =>
        new(
            Guid.NewGuid(),
            CalculatedCost: 0m,
            NormalCost: 0m,
            WeekendCost: 0m,
            HolidayCost: 0m,
            OvertimeCost: 0m,
            TotalHours: totalHours,
            WeekendHours: weekendHours,
            HolidayHours: holidayHours,
            OvertimeHours: overtimeHours,
            IsWeekend: isWeekend,
            IsHoliday: isHoliday);

    private static string[] TypesOf(EntryRow row) =>
        DimensionCatalog.GetRequired("hourType").KeysOf(row).Select(key => key.Value).ToArray();

    [Fact]
    public void WeekendOvertime_CountsUnderBothBuckets()
    {
        var row = Row(new DateOnly(2026, 7, 4), Cost(8m, weekendHours: 8m, overtimeHours: 3m, isWeekend: true));
        Assert.Equal(["Weekend", "Overtime"], TypesOf(row));
    }

    [Fact]
    public void WeekdayPartialOvertime_IsBothOvertimeAndNormal()
    {
        // 10h logged, 2h of it past the weekly threshold — the other 8h are ordinary time.
        var row = Row(new DateOnly(2026, 7, 1), Cost(10m, overtimeHours: 2m));
        Assert.Equal(["Overtime", "Normal"], TypesOf(row));
    }

    [Fact]
    public void WeekdayFullyOvertime_IsNotAlsoNormal()
    {
        var row = Row(new DateOnly(2026, 7, 1), Cost(8m, overtimeHours: 8m));
        Assert.Equal(["Overtime"], TypesOf(row));
    }

    [Fact]
    public void PlainWeekday_IsNormalOnly()
    {
        var row = Row(new DateOnly(2026, 7, 1), Cost(8m));
        Assert.Equal(["Normal"], TypesOf(row));
    }

    [Fact]
    public void Holiday_TakesTheHolidayBucket()
    {
        var row = Row(new DateOnly(2026, 7, 1), Cost(8m, holidayHours: 8m, isHoliday: true));
        Assert.Equal(["Holiday"], TypesOf(row));
    }

    [Fact]
    public void WithoutCostLines_FallsBackToTheCalendar()
    {
        // 4 July 2026 is a Saturday. Cost is only loaded when a block asks for it.
        Assert.Equal(["Weekend"], TypesOf(Row(new DateOnly(2026, 7, 4), cost: null)));
        Assert.Equal(["Normal"], TypesOf(Row(new DateOnly(2026, 7, 1), cost: null)));
    }

    [Fact]
    public void FansOut_SoBlocksCarryTheDoubleCountingFootnote()
    {
        var dimension = DimensionCatalog.GetRequired("hourType");
        Assert.True(dimension.FansOut);
        Assert.Equal(DimensionCatalog.HourTypeFanOutFootnote, dimension.FanOutNote);
    }
}

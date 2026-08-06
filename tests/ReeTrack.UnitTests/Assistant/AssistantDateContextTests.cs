using ReeTrack.Infrastructure.Assistant;
using Xunit;

namespace ReeTrack.UnitTests.Assistant;

public class AssistantDateContextTests
{
    [Fact]
    public void ResolveReferenceDate_ParsesIso_OrFallsBackToToday()
    {
        Assert.Equal(new DateOnly(2026, 8, 7), AssistantDateContext.ResolveReferenceDate("2026-08-07"));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), AssistantDateContext.ResolveReferenceDate(null));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), AssistantDateContext.ResolveReferenceDate("not-a-date"));
    }

    [Fact]
    public void FridayReference_NextWeekStartsFollowingMonday()
    {
        // Friday 2026-08-07 → this week Mon 08-03 … Sun 08-09; next week Mon 08-10
        var friday = new DateOnly(2026, 8, 7);
        var block = AssistantDateContext.BuildPromptBlock(friday, "Europe/Amsterdam", "2026-08-07T14:30");

        Assert.Contains("Today: Friday 2026-08-07", block);
        Assert.Contains("User timezone (IANA): Europe/Amsterdam", block);
        Assert.Contains("Local date-time right now: 2026-08-07T14:30", block);
        Assert.Contains("This week: Monday 2026-08-03 … Sunday 2026-08-09", block);
        Assert.Contains("Next week: Monday 2026-08-10 … Sunday 2026-08-16", block);
        Assert.Contains("Last week: Monday 2026-07-27 … Sunday 2026-08-02", block);
        Assert.Contains("Next Monday: 2026-08-10", block);
        Assert.Contains("Last Friday: 2026-07-31", block);
    }

    [Fact]
    public void MondayReference_ThisWeekMondayIsToday_NextWeekIsPlusSeven()
    {
        var monday = new DateOnly(2026, 8, 10);
        var block = AssistantDateContext.BuildPromptBlock(monday, null, null);

        Assert.Contains("Today: Monday 2026-08-10", block);
        Assert.Contains("This week: Monday 2026-08-10 … Sunday 2026-08-16", block);
        Assert.Contains("Next week: Monday 2026-08-17 … Sunday 2026-08-23", block);
        Assert.Contains("Next Monday: 2026-08-10", block);
        Assert.DoesNotContain("User timezone", block);
    }

    [Fact]
    public void NextOnOrAfter_And_MostRecentPast()
    {
        var friday = new DateOnly(2026, 8, 7);

        Assert.Equal(friday, AssistantDateContext.NextOnOrAfter(friday, DayOfWeek.Friday));
        Assert.Equal(new DateOnly(2026, 8, 10), AssistantDateContext.NextOnOrAfter(friday, DayOfWeek.Monday));
        Assert.Equal(new DateOnly(2026, 7, 31), AssistantDateContext.MostRecentPast(friday, DayOfWeek.Friday));
        Assert.Equal(new DateOnly(2026, 8, 3), AssistantDateContext.MostRecentPast(friday, DayOfWeek.Monday));
    }

    [Fact]
    public void ResolveWeekDates_NextWeekdays_StartsMondayNotSunday()
    {
        // Friday 2026-08-07 — next week must be Mon 10 … Fri 14, never Sun 09
        var friday = new DateOnly(2026, 8, 7);
        var dates = AssistantDateContext.ResolveWeekDates(friday, "next", "weekdays");

        Assert.NotNull(dates);
        Assert.Equal(
            [
                new DateOnly(2026, 8, 10),
                new DateOnly(2026, 8, 11),
                new DateOnly(2026, 8, 12),
                new DateOnly(2026, 8, 13),
                new DateOnly(2026, 8, 14),
            ],
            dates);
        Assert.DoesNotContain(new DateOnly(2026, 8, 9), dates); // Sunday
        Assert.DoesNotContain(new DateOnly(2026, 8, 8), dates); // Saturday
    }

    [Fact]
    public void ResolveWeekDates_ExplicitDays_And_All()
    {
        var monday = new DateOnly(2026, 8, 10);
        var wedFri = AssistantDateContext.ResolveWeekDates(monday, "next", "wednesday,friday");
        Assert.Equal([new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 21)], wedFri);

        var all = AssistantDateContext.ResolveWeekDates(monday, "this", "all");
        Assert.NotNull(all);
        Assert.Equal(7, all.Count);
        Assert.Equal(new DateOnly(2026, 8, 10), all[0]);
        Assert.Equal(new DateOnly(2026, 8, 16), all[6]);
    }

    [Fact]
    public void BuildPromptBlock_IncludesNextWeekPresetDates()
    {
        var block = AssistantDateContext.BuildPromptBlock(new DateOnly(2026, 8, 7), null, null);
        Assert.Contains("expandWeek=next, expandDays=weekdays → 2026-08-10, 2026-08-11, 2026-08-12, 2026-08-13, 2026-08-14", block);
        Assert.Contains("NOT Sunday–Saturday", block);
    }
}

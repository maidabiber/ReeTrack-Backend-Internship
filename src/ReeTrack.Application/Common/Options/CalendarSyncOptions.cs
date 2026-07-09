namespace ReeTrack.Application.Common.Options;

public class CalendarSyncOptions
{
    public const string SectionName = "CalendarSync";

    public int IntervalMinutes { get; set; } = 15;
    public int LookbackDays { get; set; } = 30;
    public int LookaheadDays { get; set; } = 90;
}

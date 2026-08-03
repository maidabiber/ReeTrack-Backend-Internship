namespace ReeTrack.Application.Common.Options;

public sealed class WeeklyTargetCheckInOptions
{
    public const string SectionName = "WeeklyTargetCheckIn";

    /// <summary>IANA timezone id, e.g. Europe/Zagreb.</summary>
    public string TimeZone { get; set; } = "Europe/Zagreb";

    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Friday;

    /// <summary>Local time of day as HH:mm (24h).</summary>
    public string AtLocalTime { get; set; } = "12:00";

    public int PollIntervalSeconds { get; set; } = 60;
}

namespace ReeTrack.Application.Common.Options;

public class TimeEntryOptions
{
    public const string SectionName = "TimeEntry";

    /// <summary>
    /// When set, entries starting before this UTC instant cannot be created or edited.
    /// </summary>
    public DateTime? LockedBeforeUtc { get; set; }
}

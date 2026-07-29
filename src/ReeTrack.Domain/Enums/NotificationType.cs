namespace ReeTrack.Domain.Enums;

/// <summary>
/// Categories of notifications a user can subscribe to.
/// </summary>
public enum NotificationType : short
{
    ProjectCostAlert = 0,
    TimeGoalMissed = 1,
    TimeEntryShared = 2,
    TimesheetDecision = 3
}

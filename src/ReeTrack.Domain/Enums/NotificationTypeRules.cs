namespace ReeTrack.Domain.Enums;

/// <summary>
/// Rules for how notification types interact with user preferences.
/// </summary>
public static class NotificationTypeRules
{
    /// <summary>
    /// Workflow types always include InApp delivery when that channel exists.
    /// Users cannot disable InApp for these types.
    /// </summary>
    public static bool IsInAppMandatory(NotificationType type) =>
        type is NotificationType.TimeEntryShared or NotificationType.TimesheetDecision;
}

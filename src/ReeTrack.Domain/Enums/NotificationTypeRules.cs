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
        type is NotificationType.TimeEntryShared
            or NotificationType.TimesheetDecision
            or NotificationType.ProjectThresholdAlert;

    /// <summary>
    /// Email is treated as enabled when the user has no Email preference row for the type.
    /// An explicit <c>IsEnabled = false</c> row opts out.
    /// </summary>
    public static bool IsEmailDefaultEnabled(NotificationType _) => true;
}

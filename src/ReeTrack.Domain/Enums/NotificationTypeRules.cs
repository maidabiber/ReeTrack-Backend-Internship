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
            or NotificationType.WeeklyTargetCheckIn;

    /// <summary>
    /// When no preference row exists for the channel, treat it as enabled for these pairs.
    /// Explicit opt-out (<c>IsEnabled = false</c>) is still honored.
    /// </summary>
    public static bool IsDefaultEnabledWhenUnset(NotificationType type, DeliveryChannel channel) =>
        channel is DeliveryChannel.Email
        && type is NotificationType.WeeklyTargetCheckIn;
}

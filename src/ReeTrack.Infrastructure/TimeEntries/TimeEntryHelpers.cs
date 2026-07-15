using ReeTrack.Application.Common.Exceptions;

namespace ReeTrack.Infrastructure.TimeEntries;

internal static class TimeEntryHelpers
{
    public const int MaxDurationSeconds = 24 * 60 * 60;

    public static void ValidateManualRange(DateTime startedAtUtc, DateTime endedAtUtc)
    {
        if (endedAtUtc <= startedAtUtc)
            throw new AppException("End time must be after start time.");

        var durationSeconds = (endedAtUtc - startedAtUtc).TotalSeconds;
        if (durationSeconds > MaxDurationSeconds)
            throw new AppException("Duration cannot exceed 24 hours.");
    }

    public static void ValidateDurationOnly(int durationSeconds)
    {
        if (durationSeconds <= 0)
            throw new AppException("Duration must be greater than zero.", 400);

        if (durationSeconds > MaxDurationSeconds)
            throw new AppException("Duration cannot exceed 24 hours.", 400);
    }

    public static DateTime NormalizeEntryDateUtc(DateTime entryDateUtc)
    {
        var utc = entryDateUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(entryDateUtc, DateTimeKind.Utc)
            : entryDateUtc.ToUniversalTime();

        return new DateTime(utc.Year, utc.Month, utc.Day, 12, 0, 0, DateTimeKind.Utc);
    }

    public static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var trimmed = description.Trim();
        return trimmed.Length > 1000 ? trimmed[..1000] : trimmed;
    }
}

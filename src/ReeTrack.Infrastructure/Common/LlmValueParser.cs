using System.Globalization;
using System.Text.RegularExpressions;

namespace ReeTrack.Infrastructure.Common;

/// <summary>
/// Normalizes loosely-typed string values an LLM emits (numbers, booleans, times, dates)
/// into the concrete forms callers need. Shared by SmartTimeParseService and TimeEntryAssistantTools.
/// </summary>
internal static class LlmValueParser
{
    private static readonly Regex TimeOfDayPattern = new(
        @"^(?:[01]?\d|2[0-3]):[0-5]\d$",
        RegexOptions.Compiled);
    private static readonly Regex IsoDatePattern = new(
        @"^\d{4}-\d{2}-\d{2}$",
        RegexOptions.Compiled);

    public static int ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i;

        if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Round(d);

        return 0;
    }

    public static double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : 0;
    }

    public static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var trimmed = value.Trim();
        if (bool.TryParse(trimmed, out var b))
            return b;

        return trimmed.ToLowerInvariant() switch
        {
            "1" or "yes" or "y" => true,
            "0" or "no" or "n" => false,
            _ => defaultValue
        };
    }

    public static string? NormalizeTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (!TimeOfDayPattern.IsMatch(trimmed))
            return null;

        // Normalize "9:05" → "09:05" so drafts and <input type="time"> stay consistent.
        var parts = trimmed.Split(':');
        return $"{int.Parse(parts[0], CultureInfo.InvariantCulture):00}:{parts[1]}";
    }

    public static string? NormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (!IsoDatePattern.IsMatch(trimmed))
            return null;

        return DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? trimmed
            : null;
    }
}

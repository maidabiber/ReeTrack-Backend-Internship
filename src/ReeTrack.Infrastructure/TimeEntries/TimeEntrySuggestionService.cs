using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.TimeEntries;

public class TimeEntrySuggestionService : ITimeEntrySuggestionService
{
    private const int LookbackDays = 30;
    private const int TopSuggestionCount = 5;

    // Scoring weights
    private const double FrequencyFactor = 2.0;
    private const double RecencyLessThan1Day = 50.0;
    private const double RecencyLessThan3Days = 30.0;
    private const double RecencyLessThan7Days = 15.0;
    private const double TimeOfDayWeight = 25.0;
    private const double DayOfWeekWeight = 15.0;
    private const int TimeOfDayWindowHours = 2;
    private const double DescriptionVarianceThreshold = 0.5;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TimeEntrySuggestionService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TimeEntrySuggestionDto>> GetSuggestionsAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var currentUtc = DateTime.UtcNow;
        var cutoffUtc = currentUtc.AddDays(-LookbackDays);

        // Project only the fields needed for scoring — evaluate grouping/scoring in memory.
        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(e =>
                e.UserId == userId &&
                e.StartedAtUtc != null &&
                e.StartedAtUtc >= cutoffUtc)
            .Select(e => new EntryProjection(
                e.ClientId,
                e.ProjectId,
                e.ProjectTaskId,
                e.IsBillable,
                e.Description,
                e.StartedAtUtc!.Value,
                e.EndedAtUtc,
                e.DurationSeconds,
                e.Project != null ? e.Project.Name : null,
                e.Project != null ? e.Project.Color : null,
                e.ProjectTask != null ? e.ProjectTask.Name : null))
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return [];

        return entries
            .GroupBy(e => new Signature(e.ClientId, e.ProjectId, e.ProjectTaskId, e.IsBillable))
            .Select(group => BuildSuggestion(group, currentUtc))
            .Where(HasEnoughFields)
            .OrderByDescending(s => s.Score)
            .Take(TopSuggestionCount)
            .ToList();
    }

    /// <summary>
    /// Require at least two non-null values among the structural/content fields.
    /// </summary>
    private static bool HasEnoughFields(TimeEntrySuggestionDto suggestion)
    {
        var nonNullCount = 0;
        if (suggestion.ClientId is not null) nonNullCount++;
        if (suggestion.ProjectId is not null) nonNullCount++;
        if (suggestion.ProjectTaskId is not null) nonNullCount++;
        if (!string.IsNullOrWhiteSpace(suggestion.SuggestedDescription)) nonNullCount++;
        return nonNullCount >= 2;
    }


    /// <summary>
    /// Scores a structural group and decides whether a description is predictable enough to suggest.
    /// </summary>
    private static TimeEntrySuggestionDto BuildSuggestion(
        IGrouping<Signature, EntryProjection> group,
        DateTime currentUtc)
    {
        var entries = group.ToList();
        var count = entries.Count;
        var mostRecent = entries.Max(e => e.StartedAtUtc);
        var age = currentUtc - mostRecent;

        // Frequency: more uses → higher baseline score.
        var score = count * FrequencyFactor;

        // Recency: boost groups that were used recently.
        if (age < TimeSpan.FromDays(1))
            score += RecencyLessThan1Day;
        
        else if (age < TimeSpan.FromDays(3))
            score += RecencyLessThan3Days;
        
        else if (age < TimeSpan.FromDays(7))
            score += RecencyLessThan7Days;

        // Time of day: boost when a high share of starts fall near the current UTC hour.
        var currentHour = currentUtc.Hour;
        var timeOfDayRatio = entries.Count(e =>
            HoursWithinWindow(e.StartedAtUtc.Hour, currentHour, TimeOfDayWindowHours)) / (double)count;
        score += timeOfDayRatio * TimeOfDayWeight;

        // Day of week: boost when a high share of starts fall on the current UTC weekday.
        var currentDow = currentUtc.DayOfWeek;
        var dayOfWeekRatio = entries.Count(e => e.StartedAtUtc.DayOfWeek == currentDow) / (double)count;
        score += dayOfWeekRatio * DayOfWeekWeight;

        var suggestedDescription = ResolveSuggestedDescription(entries);
        var (startTime, endTime, durationSeconds, projectName, projectColor, projectTaskName) =
            ResolveSuggestedTimesAndDisplay(entries);


        return new TimeEntrySuggestionDto(
            group.Key.ClientId,
            group.Key.ProjectId,
            group.Key.ProjectTaskId,
            group.Key.IsBillable,
            suggestedDescription,
            startTime,
            endTime,
            durationSeconds,
            score,
            projectName,
            projectColor,
            projectTaskName);
    }

    /// <summary>
    /// Suggest start/end times of day and display fields from the group's most recent entry (UTC).
    /// End time falls back to start + duration when EndedAtUtc is missing.
    /// </summary>
    private static (
        TimeOnly? Start,
        TimeOnly? End,
        int DurationSeconds,
        string? ProjectName,
        string? ProjectColor,
        string? ProjectTaskName) ResolveSuggestedTimesAndDisplay(
        IReadOnlyList<EntryProjection> entries)
    {
        var latest = entries.MaxBy(e => e.StartedAtUtc);
        if (latest is null)
            return (null, null, 0, null, null, null);

        var start = TimeOnly.FromDateTime(latest.StartedAtUtc);
        TimeOnly? end = latest.EndedAtUtc is { } ended
            ? TimeOnly.FromDateTime(ended)
            : latest.DurationSeconds > 0
                ? TimeOnly.FromDateTime(latest.StartedAtUtc.AddSeconds(latest.DurationSeconds))
                : null;

        return (
            start,
            end,
            latest.DurationSeconds,
            latest.ProjectName,
            latest.ProjectColor,
            latest.ProjectTaskName);
    }

    /// <summary>
    /// Only suggest a description when reuse is predictable (low variance).
    /// varianceRatio = unique non-empty descriptions / total entries in the group.
    /// </summary>
    private static string? ResolveSuggestedDescription(IReadOnlyList<EntryProjection> entries)
    {
        var nonEmpty = entries
            .Select(e => e.Description)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d!.Trim())
            .ToList();

        if (nonEmpty.Count == 0)
            return null;

        var uniqueCount = nonEmpty.Distinct(StringComparer.Ordinal).Count();
        var varianceRatio = uniqueCount / (double)entries.Count;


        if (varianceRatio >= DescriptionVarianceThreshold)
            return null;


        return nonEmpty
            .GroupBy(d => d, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .First()
            .Key;
    }



    /// <summary>
    /// Circular hour distance on a 24-hour clock (e.g. 23 and 1 are 2 hours apart).
    /// </summary>
    private static bool HoursWithinWindow(int hourA, int hourB, int windowHours)
    {
        var diff = Math.Abs(hourA - hourB);
        var circular = Math.Min(diff, 24 - diff);
        return circular <= windowHours;
    }



    private sealed record Signature(
        Guid? ClientId,
        Guid? ProjectId,
        Guid? ProjectTaskId,
        bool IsBillable);

    private sealed record EntryProjection(
        Guid? ClientId,
        Guid? ProjectId,
        Guid? ProjectTaskId,
        bool IsBillable,
        string? Description,
        DateTime StartedAtUtc,
        DateTime? EndedAtUtc,
        int DurationSeconds,
        string? ProjectName,
        string? ProjectColor,
        string? ProjectTaskName);
}
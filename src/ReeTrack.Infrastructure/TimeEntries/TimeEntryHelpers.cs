using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.TimeEntries;

internal static class TimeEntryHelpers
{
    public const int MaxDurationSeconds = 24 * 60 * 60;

    /// <summary>
    /// UTC [start, end) for a local calendar day. <paramref name="utcOffsetMinutes"/> is
    /// <c>Date#getTimezoneOffset()</c>: minutes to add to local wall time to get UTC.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtc) GetLocalDayUtcRange(
        DateOnly day,
        int utcOffsetMinutes)
    {
        var fromUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(utcOffsetMinutes);
        var toUtc = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(utcOffsetMinutes);
        return (fromUtc, toUtc);
    }

    /// <summary>
    /// UTC [start, end) for the local calendar day that contains <paramref name="instantUtc"/>.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtc) GetLocalDayUtcRange(
        DateTime instantUtc,
        int utcOffsetMinutes)
    {
        var local = instantUtc.AddMinutes(-utcOffsetMinutes);
        var day = DateOnly.FromDateTime(local);
        return GetLocalDayUtcRange(day, utcOffsetMinutes);
    }

    public static async Task<IReadOnlyDictionary<Guid, List<TimeEntry>>> LoadShareGroupsAsync(
        IApplicationDbContext db,
        IReadOnlyList<TimeEntry> entries,
        CancellationToken cancellationToken)
    {
        var shareGroupIds = entries
            .Where(e => e.ShareGroupId is not null)
            .Select(e => e.ShareGroupId!.Value)
            .Distinct()
            .ToList();

        if (shareGroupIds.Count == 0)
            return new Dictionary<Guid, List<TimeEntry>>();

        var siblings = await db.TimeEntries
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.SubmittedByUser)
            .Where(e => e.ShareGroupId != null && shareGroupIds.Contains(e.ShareGroupId.Value))
            .ToListAsync(cancellationToken);

        return siblings
            .GroupBy(e => e.ShareGroupId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
    }
}

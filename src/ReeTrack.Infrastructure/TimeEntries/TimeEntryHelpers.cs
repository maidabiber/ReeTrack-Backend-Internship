using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.TimeEntries;

internal static class TimeEntryHelpers
{
    public const int MaxDurationSeconds = 24 * 60 * 60;

    public static async Task<string?> FindOverlapMessageAsync(
        IApplicationDbContext db,
        Guid userId,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        Guid? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var overlapping = await db.TimeEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.StartedAtUtc != null)
            .Where(e => excludeEntryId == null || e.Id != excludeEntryId)
            .Where(e =>
                e.StartedAtUtc < endedAtUtc &&
                (e.EndedAtUtc ?? now) > startedAtUtc)
            .OrderBy(e => e.StartedAtUtc)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (overlapping.Count == 0)
            return null;

        var labels = overlapping
            .Select(e => e.Description?.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Take(2)
            .ToList();

        if (labels.Count > 0)
            return $"This entry overlaps with: {string.Join(", ", labels)}.";

        return "This entry overlaps with an existing time entry.";
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

using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.TimeEntries;

internal static class TimeEntryHelpers
{
    public const int MaxDurationSeconds = 24 * 60 * 60;

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

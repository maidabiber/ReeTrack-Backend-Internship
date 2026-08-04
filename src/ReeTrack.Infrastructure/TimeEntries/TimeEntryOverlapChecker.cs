using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.TimeEntries;

public class TimeEntryOverlapChecker : ITimeEntryOverlapChecker
{
    private readonly IApplicationDbContext _db;

    public TimeEntryOverlapChecker(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task EnsureNoOverlapAsync(
        Guid userId,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        Guid? excludeEntryId,
        CancellationToken cancellationToken = default)
    {
        var overlapping = await FindOverlapsAsync(userId, startedAtUtc, endedAtUtc, excludeEntryId, cancellationToken);
        if (overlapping.Count == 0)
            return;

        throw new AppException(BuildOverlapMessage(overlapping), 409, ErrorCode.EntryOverlap);
    }

    public async Task<IReadOnlyList<OverlapEntryDto>> FindOverlapsAsync(
        Guid userId,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        Guid? excludeEntryId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var overlapping = await _db.TimeEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.StartedAtUtc != null)
            .Where(e => e.Mode != TimeEntryMode.DurationOnly)
            .Where(e => excludeEntryId == null || e.Id != excludeEntryId)
            .Where(e =>
                e.StartedAtUtc < endedAtUtc &&
                (e.EndedAtUtc ?? now) > startedAtUtc)
            .OrderBy(e => e.StartedAtUtc)
            .Select(e => new
            {
                e.Id,
                e.Description,
                StartedAtUtc = e.StartedAtUtc!.Value,
                e.EndedAtUtc
            })
            .ToListAsync(cancellationToken);

        return overlapping
            .Select(e => new OverlapEntryDto
            {
                Id = e.Id,
                Description = e.Description,
                StartedAtUtc = e.StartedAtUtc,
                EndedAtUtc = e.EndedAtUtc
            })
            .ToList();
    }

    internal static string BuildOverlapMessage(IReadOnlyList<OverlapEntryDto> overlapping)
    {
        var labels = overlapping
            .Select(e => e.Description?.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Take(2)
            .ToList();

        if (labels.Count > 0)
            return $"This entry overlaps with: {string.Join(", ", labels)}.";

        return "This entry overlaps with an existing time entry.";
    }
}

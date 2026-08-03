using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.TimeEntries;

public class DailyTimeBudget : IDailyTimeBudget
{
    private readonly IApplicationDbContext _db;

    public DailyTimeBudget(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task EnsureWithinBudgetAsync(
        Guid userId,
        DateTime dateUtc,
        int newDurationSeconds,
        Guid? excludeEntryId,
        CancellationToken cancellationToken = default)
    {
        var dayStart = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var existingSeconds = await _db.TimeEntries
            .AsNoTracking()
            .Where(e =>
                e.UserId == userId &&
                e.StartedAtUtc != null &&
                e.StartedAtUtc >= dayStart &&
                e.StartedAtUtc < dayEnd &&
                e.Status != TimeEntryStatus.Pending &&
                (excludeEntryId == null || e.Id != excludeEntryId))
            .SumAsync(e => e.DurationSeconds, cancellationToken);

        var total = existingSeconds + newDurationSeconds;
        if (total > TimeEntryHelpers.MaxDurationSeconds)
            throw new AppException(
                $"Total tracked time for this day would exceed 24 hours ({total / 3600.0:F1}h).",
                400,
                ErrorCode.DurationLimitExceeded);
    }
}

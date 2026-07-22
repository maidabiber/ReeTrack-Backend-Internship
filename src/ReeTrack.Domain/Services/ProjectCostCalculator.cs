using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Services;

public sealed class ProjectCostCalculator : IProjectCostCalculator
{
    private readonly IReadOnlyList<IRateMultiplier> _multipliers;

    public ProjectCostCalculator(IEnumerable<IRateMultiplier> multipliers)
    {
        _multipliers = multipliers
            .OrderBy(m => m.ExecutionOrder)
            .ToList();
    }

    public decimal Calculate(
        Project project,
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<UserHourlyRate> userRates)
    {
        var projectRate = project.HourlyRate ?? 0m;
        decimal total = 0m;

        foreach (var entry in entries)
        {
            if (entry.Status != TimeEntryStatus.Confirmed) // Already filtered in the service, but double-checking here for safety
                continue;

            if (entry.DeletedAtUtc is not null)
                continue;

            var entryDate = ResolveEntryDate(entry);
            var userRate = ResolveUserRate(userRates, entry.UserId, entryDate);
            var baseRate = Math.Max(userRate, projectRate);

            var context = new RateContext(entry, entryDate, baseRate);
            var appliedRate = baseRate;
            foreach (var multiplier in _multipliers)
                appliedRate = multiplier.Apply(appliedRate, context); 
                // Will be used later for holidays, weekends, overtime, etc. 
                // The multipliers are applied in the order defined by ExecutionOrder.

            total += (entry.DurationSeconds / 3600m) * appliedRate;
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static DateOnly ResolveEntryDate(TimeEntry entry)
    {
        var instant = entry.StartedAtUtc ?? entry.CreatedAtUtc;
        return DateOnly.FromDateTime(instant);
    }

    private static decimal ResolveUserRate(
        IReadOnlyList<UserHourlyRate> userRates,
        Guid userId,
        DateOnly entryDate)
    {
        var rate = userRates.FirstOrDefault(r => r.UserId == userId && r.Covers(entryDate));
        return rate?.Rate.Amount ?? 0m;
    }
}

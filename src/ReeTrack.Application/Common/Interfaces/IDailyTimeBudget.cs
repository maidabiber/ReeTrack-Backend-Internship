namespace ReeTrack.Application.Common.Interfaces;

public interface IDailyTimeBudget
{
    Task EnsureWithinBudgetAsync(
        Guid userId,
        DateTime dateUtc,
        int newDurationSeconds,
        Guid? excludeEntryId,
        CancellationToken cancellationToken = default);
}

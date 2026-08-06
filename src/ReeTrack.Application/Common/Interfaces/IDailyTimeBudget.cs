namespace ReeTrack.Application.Common.Interfaces;

public interface IDailyTimeBudget
{
    /// <param name="utcOffsetMinutes">
    /// Client <c>Date#getTimezoneOffset()</c> so the 24h cap uses the local calendar day.
    /// Zero keeps UTC-day behavior for clients that omit the offset.
    /// </param>
    Task EnsureWithinBudgetAsync(
        Guid userId,
        DateTime dateUtc,
        int newDurationSeconds,
        Guid? excludeEntryId,
        int utcOffsetMinutes = 0,
        CancellationToken cancellationToken = default);
}

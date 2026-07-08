namespace ReeTrack.Application.Common.Interfaces;

public interface ILockedPeriodService
{
    Task EnsureEntryEditableAsync(
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default);
}

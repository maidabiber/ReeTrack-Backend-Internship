namespace ReeTrack.Application.Common.Interfaces;

public interface ITimeEntryOverlapChecker
{
    Task EnsureNoOverlapAsync(
        Guid userId,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        Guid? excludeEntryId,
        CancellationToken cancellationToken = default);
}

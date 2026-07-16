namespace ReeTrack.Application.Common.Interfaces;

/// <summary>
/// Single choke point for time-entry mutations: rejects edits in globally locked
/// periods (403) and in weeks covered by a submitted/approved timesheet (409).
/// </summary>
public interface ITimeEntryGuardService
{
    Task EnsureEditableAsync(
        Guid ownerUserId,
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default);
}

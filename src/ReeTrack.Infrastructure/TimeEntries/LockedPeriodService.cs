using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;

namespace ReeTrack.Infrastructure.TimeEntries;

public class LockedPeriodService : ILockedPeriodService
{
    private readonly TimeEntryOptions _options;

    public LockedPeriodService(IOptions<TimeEntryOptions> options)
    {
        _options = options.Value;
    }

    public Task EnsureEntryEditableAsync(
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var lockedBefore = _options.LockedBeforeUtc;
        if (lockedBefore is not null && startedAtUtc < lockedBefore)
        {
            throw new AppException("This time period is locked and cannot be edited.", 403);
        }

        return Task.CompletedTask;
    }
}

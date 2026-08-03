using ReeTrack.Application.Common.Options;

namespace ReeTrack.Application.Common.Interfaces;

public interface IWeeklyTargetCheckInJob
{
    /// <summary>
    /// Runs the Friday check-in for the local week of <paramref name="utcNow"/> in the configured timezone.
    /// No-ops when already recorded for that local Monday.
    /// </summary>
    Task RunAsync(DateTime utcNow, WeeklyTargetCheckInOptions options, CancellationToken cancellationToken = default);
}

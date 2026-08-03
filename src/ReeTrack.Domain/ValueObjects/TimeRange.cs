using ReeTrack.Domain.Exceptions;

namespace ReeTrack.Domain.ValueObjects;

public sealed class TimeRange
{
    public const int MaxDurationSeconds = 24 * 60 * 60;

    public DateTime StartedAtUtc { get; }
    public DateTime EndedAtUtc { get; }
    public int DurationSeconds { get; }

    private TimeRange(DateTime startedAtUtc, DateTime endedAtUtc, int durationSeconds)
    {
        StartedAtUtc = startedAtUtc;
        EndedAtUtc = endedAtUtc;
        DurationSeconds = durationSeconds;
    }

    public static TimeRange Create(DateTime startedAtUtc, DateTime endedAtUtc)
    {
        if (endedAtUtc <= startedAtUtc)
            throw new DomainException("End time must be after start time.");

        var durationSeconds = (int)(endedAtUtc - startedAtUtc).TotalSeconds;
        if (durationSeconds > MaxDurationSeconds)
            throw new DomainException("Duration cannot exceed 24 hours.");

        return new TimeRange(startedAtUtc, endedAtUtc, durationSeconds);
    }
}

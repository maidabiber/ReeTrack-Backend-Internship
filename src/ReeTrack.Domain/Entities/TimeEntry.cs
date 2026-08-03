using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Exceptions;
using ReeTrack.Domain.ValueObjects;

namespace ReeTrack.Domain.Entities;

public class TimeEntry : BaseEntity, ISoftDeletable, IAuditable
{
    public Guid UserId { get; set; }

    public string? Description { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProjectTaskId { get; set; }
    public bool IsBillable { get; set; }

    public TimeEntryMode Mode { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int DurationSeconds { get; set; }

    public TimeEntryStatus Status { get; set; } = TimeEntryStatus.Confirmed;
    public Guid? SubmittedByUserId { get; set; }
    public Guid? ShareGroupId { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public User User { get; set; } = null!;
    public User? SubmittedByUser { get; set; }
    public Client? Client { get; set; }
    public Project? Project { get; set; }
    public ProjectTask? ProjectTask { get; set; }
    public ICollection<TimeEntryTag> TimeEntryTags { get; set; } = [];

    public static TimeEntry CreateManual(
        Guid userId,
        TimeRange range,
        string? description,
        bool isBillable,
        DateTime now)
    {
        return new TimeEntry
        {
            UserId = userId,
            Description = NormalizeDescription(description),
            IsBillable = isBillable,
            Mode = TimeEntryMode.Manual,
            StartedAtUtc = range.StartedAtUtc,
            EndedAtUtc = range.EndedAtUtc,
            DurationSeconds = range.DurationSeconds,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public static TimeEntry CreateDurationOnly(
        Guid userId,
        int durationSeconds,
        DateTime entryDateUtc,
        string? description,
        bool isBillable,
        DateTime now)
    {
        ValidateDurationOnly(durationSeconds);
        var normalizedDate = NormalizeEntryDateUtc(entryDateUtc);

        return new TimeEntry
        {
            UserId = userId,
            Description = NormalizeDescription(description),
            IsBillable = isBillable,
            Mode = TimeEntryMode.DurationOnly,
            StartedAtUtc = normalizedDate,
            EndedAtUtc = null,
            DurationSeconds = durationSeconds,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public static TimeEntry CreateTimer(
        Guid userId,
        string? description,
        bool isBillable,
        DateTime now)
    {
        return new TimeEntry
        {
            UserId = userId,
            Description = NormalizeDescription(description),
            IsBillable = isBillable,
            Mode = TimeEntryMode.Timer,
            StartedAtUtc = now,
            EndedAtUtc = null,
            DurationSeconds = 0,
            Status = TimeEntryStatus.Confirmed,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Stop(DateTime endedAtUtc)
    {
        if (Mode != TimeEntryMode.Timer)
            throw new DomainException("Only timer entries can be stopped.");

        if (EndedAtUtc is not null)
            throw new DomainException("This timer has already been stopped.");

        if (endedAtUtc <= StartedAtUtc)
            throw new DomainException("End time must be after start time.");

        EndedAtUtc = endedAtUtc;
        DurationSeconds = (int)Math.Max(0, (endedAtUtc - StartedAtUtc!.Value).TotalSeconds);
    }

    public void UpdateTiming(TimeRange range)
    {
        if (Mode == TimeEntryMode.Timer && EndedAtUtc is null)
            throw new DomainException("Cannot edit a running timer entry.");

        if (Mode == TimeEntryMode.DurationOnly)
            throw new DomainException("Duration-only entries cannot be updated with start/end times. Use UpdateDuration.");

        StartedAtUtc = range.StartedAtUtc;
        EndedAtUtc = range.EndedAtUtc;
        DurationSeconds = range.DurationSeconds;
    }

    public void UpdateDuration(int seconds, DateTime entryDate)
    {
        if (Mode != TimeEntryMode.DurationOnly)
            throw new DomainException("Only duration-only entries can be updated with a duration value.");

        ValidateDurationOnly(seconds);

        var normalizedDate = NormalizeEntryDateUtc(entryDate);
        StartedAtUtc = normalizedDate;
        EndedAtUtc = null;
        DurationSeconds = seconds;
    }

    public void UpdateDetails(string? description, bool? isBillable)
    {
        Description = NormalizeDescription(description);
        if (isBillable is bool billable)
            IsBillable = billable;
    }

    public TimeEntry ShareWith(
        Guid assigneeUserId,
        Guid submittedByUserId,
        Guid shareGroupId,
        DateTime now)
    {
        return new TimeEntry
        {
            UserId = assigneeUserId,
            Description = Description,
            IsBillable = IsBillable,
            Mode = Mode,
            Status = TimeEntryStatus.Pending,
            SubmittedByUserId = submittedByUserId,
            ShareGroupId = shareGroupId,
            StartedAtUtc = StartedAtUtc,
            EndedAtUtc = EndedAtUtc,
            DurationSeconds = DurationSeconds,
            ClientId = ClientId,
            ProjectId = ProjectId,
            ProjectTaskId = ProjectTaskId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public DateTime GetEntryDate() =>
        StartedAtUtc ?? NormalizeEntryDateUtc(DateTime.UtcNow);

    public static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var trimmed = description.Trim();
        return trimmed.Length > 1000 ? trimmed[..1000] : trimmed;
    }

    public static DateTime NormalizeEntryDateUtc(DateTime entryDateUtc)
    {
        var utc = entryDateUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(entryDateUtc, DateTimeKind.Utc)
            : entryDateUtc.ToUniversalTime();

        return new DateTime(utc.Year, utc.Month, utc.Day, 12, 0, 0, DateTimeKind.Utc);
    }

    private static void ValidateDurationOnly(int durationSeconds)
    {
        if (durationSeconds <= 0)
            throw new DomainException("Duration must be greater than zero.");

        if (durationSeconds > TimeRange.MaxDurationSeconds)
            throw new DomainException("Duration cannot exceed 24 hours.");
    }
}

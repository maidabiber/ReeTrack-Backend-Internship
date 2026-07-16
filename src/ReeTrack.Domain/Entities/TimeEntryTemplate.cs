using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class TimeEntryTemplate : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Source time entry this template was created from; unique to prevent duplicates.</summary>
    public Guid TimeEntryId { get; set; }

    public Guid? ProjectId { get; set; }
    public Guid? ProjectTaskId { get; set; }
    public string? Description { get; set; }
    public bool IsBillable { get; set; }

    /// <summary>UTC time-of-day from the source entry; null for duration-only templates.</summary>
    public TimeOnly? StartTimeUtc { get; set; }

    /// <summary>UTC time-of-day from the source entry; null for duration-only templates.</summary>
    public TimeOnly? EndTimeUtc { get; set; }

    public int DurationSeconds { get; set; }

    public User User { get; set; } = null!;
    public TimeEntry TimeEntry { get; set; } = null!;
    public Project? Project { get; set; }
    public ProjectTask? ProjectTask { get; set; }

    public static TimeEntryTemplate FromTimeEntry(Guid userId, TimeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null)
            throw new InvalidOperationException("Cannot create a template from a running timer.");

        TimeOnly? startTime = null;
        TimeOnly? endTime = null;

        if (entry.Mode != TimeEntryMode.DurationOnly)
        {
            if (entry.StartedAtUtc is null)
                throw new InvalidOperationException("Time entry is missing a start time.");

            startTime = TimeOnly.FromDateTime(entry.StartedAtUtc.Value);
            endTime = entry.EndedAtUtc is null
                ? null
                : TimeOnly.FromDateTime(entry.EndedAtUtc.Value);
        }

        return new TimeEntryTemplate
        {
            UserId = userId,
            TimeEntryId = entry.Id,
            ProjectId = entry.ProjectId,
            ProjectTaskId = entry.ProjectTaskId,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
            DurationSeconds = entry.DurationSeconds
        };
    }
}

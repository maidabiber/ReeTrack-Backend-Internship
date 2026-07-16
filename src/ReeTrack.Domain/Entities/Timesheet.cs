using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class Timesheet : BaseEntity, IAuditable
{
    public Guid UserId { get; set; }

    /// <summary>UTC Monday of the week this timesheet covers.</summary>
    public DateOnly WeekStartDate { get; set; }

    public TimesheetStatus Status { get; set; } = TimesheetStatus.Submitted;
    public DateTime SubmittedAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewComment { get; set; }

    public User User { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}

using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class User : BaseEntity, IAuditable
{
    public string Email { get; set; } = string.Empty;
    public string? GoogleSub { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserRole> AssignedRoles { get; set; } = [];
    public ICollection<Invitation> SentInvitations { get; set; } = [];
    public ICollection<Invitation> AcceptedInvitations { get; set; } = [];
    public ICollection<TimeEntry> TimeEntries { get; set; } = [];
    public ICollection<ProjectTask> AssignedTasks { get; set; } = [];
}

using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Models;

public sealed class MemberDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required string Role { get; init; }
    public required short RoleId { get; init; }
    public required UserStatus Status { get; init; }
    public required bool EmailVerified { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
    public Guid? PendingInvitationId { get; init; }
    public HourTargetMode? HourTargetMode { get; init; }
    public decimal? HourTargetHours { get; init; }
}

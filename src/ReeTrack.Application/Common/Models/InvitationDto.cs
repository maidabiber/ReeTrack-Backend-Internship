using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Models;

public sealed class InvitationDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required short RoleId { get; init; }
    public required InvitationStatus Status { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required Guid InvitedByUserId { get; init; }
}

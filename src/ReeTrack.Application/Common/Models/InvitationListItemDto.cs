namespace ReeTrack.Application.Common.Models;

/// <summary>
/// A row in the admin invitations list. Status is the effective status: a
/// Pending invitation past its expiry reports as Expired (nothing writes the
/// Expired value to the database; it is computed at read time).
/// </summary>
public sealed class InvitationListItemDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required short RoleId { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required string InvitedByName { get; init; }
    public DateTime? AcceptedAtUtc { get; init; }
}

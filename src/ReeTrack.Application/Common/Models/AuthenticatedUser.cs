namespace ReeTrack.Application.Common.Models;

public sealed class AuthenticatedUser
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}

namespace ReeTrack.Application.Common.Models;

public sealed class TeammateDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
}

namespace ReeTrack.Application.Common.Models;

public sealed class ClientDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required int ProjectCount { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

namespace ReeTrack.Application.Common.Models;

public sealed class TagDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string? Color { get; init; }
    public required int UsageCount { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

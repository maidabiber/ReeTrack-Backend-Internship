namespace ReeTrack.Application.Common.Models;

public sealed class TimeEntryTagDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Color { get; init; }
}

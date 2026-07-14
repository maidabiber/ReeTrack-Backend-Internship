namespace ReeTrack.Api.Contracts;

public sealed class CreateTagRequest
{
    public string? Name { get; set; }
    public string? Color { get; set; }
}

public sealed class UpdateTagRequest
{
    public string? Name { get; set; }

    // Sentinel: omit to leave the color unchanged; send "" to clear it.
    public string? Color { get; set; }
}

public sealed class TagResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string? Color { get; init; }
    public required int UsageCount { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

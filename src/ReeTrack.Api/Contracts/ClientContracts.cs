namespace ReeTrack.Api.Contracts;

public sealed class CreateClientRequest
{
    public string? Name { get; set; }
}

public sealed class UpdateClientRequest
{
    public string? Name { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class ClientResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required int ProjectCount { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

namespace ReeTrack.Api.Contracts;

public sealed class TeammateResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
}

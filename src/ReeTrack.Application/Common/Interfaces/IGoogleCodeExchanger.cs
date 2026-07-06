namespace ReeTrack.Application.Common.Interfaces;

public interface IGoogleCodeExchanger
{
    Task<GoogleTokenPayload> ExchangeAsync(string code, CancellationToken cancellationToken = default);
}

public sealed class GoogleTokenPayload
{
    public required string Subject { get; init; }
    public required string Email { get; init; }
    public required bool EmailVerified { get; init; }
    public string? Name { get; init; }
    public string? Picture { get; init; }
}

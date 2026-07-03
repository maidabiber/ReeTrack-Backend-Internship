namespace ReeTrack.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthResult> SignInWithGoogleAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed class AuthResult
{
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required AuthenticatedUser User { get; init; }
}

public sealed class AuthenticatedUser
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}

public sealed class AuthException : Exception
{
    public int StatusCode { get; }

    public AuthException(string message, int statusCode = 401)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

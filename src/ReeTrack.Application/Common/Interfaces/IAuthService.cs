using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthResult> SignInWithGoogleAsync(string code, CancellationToken cancellationToken = default);

    Task<AuthenticatedUser> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AuthResult
{
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required AuthenticatedUser User { get; init; }
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

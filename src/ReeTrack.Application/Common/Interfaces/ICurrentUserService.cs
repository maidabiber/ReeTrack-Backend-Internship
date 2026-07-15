namespace ReeTrack.Application.Common.Interfaces;

public interface ICurrentUserService
{
    /// <summary>
    /// The authenticated user's id. Throws an <c>AppException</c> (401) when no
    /// authenticated user is resolvable, so ambient consumers (interceptors,
    /// background code) must check <see cref="IsAuthenticated"/> before reading.
    /// </summary>
    Guid UserId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
}

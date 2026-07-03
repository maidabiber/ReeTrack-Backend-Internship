using ReeTrack.Domain.Entities;

namespace ReeTrack.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string CreateAccessToken(User user, IReadOnlyList<string> roles, out DateTime expiresAtUtc);
}

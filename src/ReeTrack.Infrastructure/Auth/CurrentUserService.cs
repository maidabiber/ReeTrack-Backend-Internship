using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Infrastructure.Auth;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var subject = _httpContextAccessor.HttpContext?.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(subject, out var userId))
                throw AppErrors.Unauthorized("Authentication is required.");

            return userId;
        }
    }

    public IReadOnlyList<string> Roles =>
        _httpContextAccessor.HttpContext?.User
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToList()
        ?? [];

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}

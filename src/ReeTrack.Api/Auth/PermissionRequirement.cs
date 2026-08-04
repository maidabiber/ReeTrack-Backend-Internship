using Microsoft.AspNetCore.Authorization;

namespace ReeTrack.Api.Auth;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

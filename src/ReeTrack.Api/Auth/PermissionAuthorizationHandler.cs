using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ReeTrack.Application.Common.Authorization;

namespace ReeTrack.Api.Auth;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var roles = context.User
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToList();

        if (PermissionMatrix.HasPermission(roles, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

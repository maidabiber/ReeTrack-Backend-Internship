using ReeTrack.Application.Common.Authorization;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Infrastructure.Auth;

public class PermissionService : IPermissionService
{
    private readonly ICurrentUserService _currentUser;

    public PermissionService(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public bool HasPermission(string permission) =>
        PermissionMatrix.HasPermission(_currentUser.Roles, permission);

    public IReadOnlyList<string> GetPermissions() =>
        PermissionMatrix.PermissionsForRoles(_currentUser.Roles);
}

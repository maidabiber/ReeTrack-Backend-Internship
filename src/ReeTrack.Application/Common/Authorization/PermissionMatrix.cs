using ReeTrack.Application.Common.Constants;

namespace ReeTrack.Application.Common.Authorization;


public static class PermissionMatrix
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> RolePermissions =
        new Dictionary<string, HashSet<string>>
        {
            [RoleNames.Member] = [],
            [RoleNames.ProjectManager] =
            [
                Permissions.ReportsView,
                Permissions.MembersView,
                Permissions.ProjectsManage,
                Permissions.InvoicesManage
            ],
            [RoleNames.Admin] = Permissions.All.ToHashSet()
        };

    public static bool HasPermission(IReadOnlyList<string> roles, string permission)
    {
        foreach (var role in roles)
        {
            if (RolePermissions.TryGetValue(role, out var permissions) &&
                permissions.Contains(permission))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> PermissionsForRoles(IReadOnlyList<string> roles)
    {
        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (RolePermissions.TryGetValue(role, out var permissions))
                granted.UnionWith(permissions);
        }

        return granted.OrderBy(p => p, StringComparer.Ordinal).ToList();
    }
}

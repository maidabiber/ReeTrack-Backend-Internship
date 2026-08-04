namespace ReeTrack.Application.Common.Interfaces;

public interface IPermissionService
{
    bool HasPermission(string permission);

    IReadOnlyList<string> GetPermissions();
}

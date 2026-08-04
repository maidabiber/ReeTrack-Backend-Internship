using ReeTrack.Application.Common.Authorization;
using ReeTrack.Application.Common.Constants;
using Xunit;

namespace ReeTrack.UnitTests.Authorization;

public class PermissionMatrixTests
{
    [Theory]
    [InlineData(RoleNames.Member, Permissions.ReportsView, false)]
    [InlineData(RoleNames.ProjectManager, Permissions.ReportsView, true)]
    [InlineData(RoleNames.Admin, Permissions.ReportsView, true)]
    [InlineData(RoleNames.Member, Permissions.TimesheetReview, false)]
    [InlineData(RoleNames.ProjectManager, Permissions.TimesheetReview, true)]
    [InlineData(RoleNames.ProjectManager, Permissions.MembersManage, false)]
    [InlineData(RoleNames.Admin, Permissions.MembersManage, true)]
    [InlineData(RoleNames.ProjectManager, Permissions.ProjectsManage, true)]
    [InlineData(RoleNames.Member, Permissions.ProjectsManage, false)]
    [InlineData(RoleNames.ProjectManager, Permissions.BillableRatesManage, true)]
    [InlineData(RoleNames.Member, Permissions.BillableRatesManage, false)]
    [InlineData(RoleNames.ProjectManager, Permissions.InvoicesManage, true)]
    [InlineData(RoleNames.Member, Permissions.InvoicesManage, false)]
    [InlineData(RoleNames.Admin, Permissions.InvoicesManage, true)]
    public void HasPermission_matches_matrix(string role, string permission, bool expected)
    {
        var result = PermissionMatrix.HasPermission([role], permission);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PermissionsForRoles_admin_includes_every_permission()
    {
        var permissions = PermissionMatrix.PermissionsForRoles([RoleNames.Admin]);
        Assert.Equal(Permissions.All.OrderBy(p => p).ToList(), permissions);
    }

    [Fact]
    public void PermissionsForRoles_project_manager_includes_middle_tier_only()
    {
        var permissions = PermissionMatrix.PermissionsForRoles([RoleNames.ProjectManager]);
        Assert.Equal(
            new[]
            {
                Permissions.BillableRatesManage,
                Permissions.InvoicesManage,
                Permissions.ProjectsManage,
                Permissions.ReportsView,
                Permissions.TimesheetReview
            }.OrderBy(p => p).ToList(),
            permissions);
    }

    [Fact]
    public void PermissionsForRoles_member_is_empty()
    {
        var permissions = PermissionMatrix.PermissionsForRoles([RoleNames.Member]);
        Assert.Empty(permissions);
    }
}

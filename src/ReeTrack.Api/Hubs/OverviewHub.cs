using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Api.Hubs;

/// <summary>
/// Real-time overview push endpoint. Server-to-client only; no client-invoked methods.
/// Clients are assigned to groups on connect based on their role:
/// - Admins join "overview:admins"
/// - ProjectManagers join "overview:pm:{userId}"
/// </summary>
[Authorize(Policy = Permissions.Policies.ReportsView)]
public sealed class OverviewHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is not null)
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst("sub")?.Value;
            var roles = httpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            if (roles.Contains(RoleNames.Admin))
                await Groups.AddToGroupAsync(Context.ConnectionId, "overview:admins");

            if (roles.Contains(RoleNames.ProjectManager) && userId is not null)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"overview:pm:{userId}");
        }

        await base.OnConnectedAsync();
    }
}

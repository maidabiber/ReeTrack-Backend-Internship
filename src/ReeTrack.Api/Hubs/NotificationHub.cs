using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ReeTrack.Api.Hubs;

/// <summary>
/// Real-time notification push endpoint. Server-to-client only; no client-invoked methods.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub;

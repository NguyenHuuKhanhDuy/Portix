using Microsoft.AspNetCore.SignalR;

namespace Portix.Client.Inspector;

/// <summary>Realtime push to the dashboard SPA: tunnel status changes and newly captured requests.</summary>
public sealed class DashboardHub : Hub
{
}

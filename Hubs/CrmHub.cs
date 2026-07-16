using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TelecallingCRM.Hubs;

/// <summary>
/// Real-time hub for:
///  - Live call status and lead assignment notifications
///  - AI assistant streaming
///  - Real-time dashboard stat pushes (DashboardUpdated)
///  - Agent presence broadcasts (AgentPresenceChanged)
/// Requires authentication and validates that the user may only join their own tenant group.
/// </summary>
[Authorize]
public class CrmHub : Hub
{
    /// <summary>Agent joins their own tenant group on connect.</summary>
    public async Task JoinTenantGroup(string tenantId)
    {
        var userTenantId = Context.User?.FindFirst("tenant_id")?.Value;
        var isSuperAdmin = Context.User?.IsInRole("superadmin") ?? false;

        if (!isSuperAdmin && userTenantId != tenantId)
            throw new HubException("Unauthorized: cannot join a different tenant group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");

        // Notify others that this agent came online
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Clients.OthersInGroup($"tenant-{tenantId}")
                .SendAsync("AgentPresenceChanged", new { agentId = userId, isOnline = true });
    }

    public async Task LeaveTenantGroup(string tenantId)
    {
        var userTenantId = Context.User?.FindFirst("tenant_id")?.Value;
        var isSuperAdmin = Context.User?.IsInRole("superadmin") ?? false;

        if (!isSuperAdmin && userTenantId != tenantId)
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        var userId   = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(userId))
            await Clients.Group($"tenant-{tenantId}")
                .SendAsync("AgentPresenceChanged", new { agentId = userId, isOnline = false });

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Server-callable: push a live dashboard update to the entire tenant group.</summary>
    public async Task PushDashboardUpdate(string tenantId, object payload)
    {
        var userTenantId = Context.User?.FindFirst("tenant_id")?.Value;
        var isSuperAdmin = Context.User?.IsInRole("superadmin") ?? false;

        if (!isSuperAdmin && userTenantId != tenantId)
            throw new HubException("Unauthorized.");

        await Clients.Group($"tenant-{tenantId}").SendAsync("DashboardUpdated", payload);
    }
}

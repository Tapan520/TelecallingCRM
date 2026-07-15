using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TelecallingCRM.Hubs;

/// <summary>
/// Real-time hub for live call status, lead assignment notifications,
/// and AI assistant streaming. Requires authentication and validates
/// that the user may only join their own tenant group.
/// </summary>
[Authorize]
public class CrmHub : Hub
{
    /// <summary>Agent joins their own tenant group on connect.</summary>
    public async Task JoinTenantGroup(string tenantId)
    {
        // Validate the requested tenantId matches the user's own claim
        var userTenantId = Context.User?.FindFirst("tenant_id")?.Value;
        var isSuperAdmin = Context.User?.IsInRole("superadmin") ?? false;

        if (!isSuperAdmin && userTenantId != tenantId)
        {
            throw new HubException("Unauthorized: cannot join a different tenant group.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
    }

    public async Task LeaveTenantGroup(string tenantId)
    {
        var userTenantId = Context.User?.FindFirst("tenant_id")?.Value;
        var isSuperAdmin = Context.User?.IsInRole("superadmin") ?? false;

        if (!isSuperAdmin && userTenantId != tenantId)
            return; // silently ignore cross-tenant leave attempts

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
    }
}

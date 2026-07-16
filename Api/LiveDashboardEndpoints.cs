using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Hubs;
using TelecallingCRM.Services;
using Microsoft.AspNetCore.SignalR;

namespace TelecallingCRM.Api;

public static class LiveDashboardEndpoints
{
    public static void MapLiveDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/live-dashboard").WithTags("LiveDashboard")
            .RequireAuthorization().RequireRateLimiting("api");

        // GET /api/live-dashboard/stats  — current live stats snapshot
        group.MapGet("/stats", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var today = DateTime.UtcNow.Date;

            var callsToday = await db.Calls
                .CountAsync(c => c.TenantId == tc.TenantId && c.StartedAt >= today);

            var conversionsToday = await db.Calls
                .CountAsync(c => c.TenantId == tc.TenantId && c.StartedAt >= today
                              && c.Outcome == CallOutcome.Converted);

            var openFollowUps = await db.FollowUps
                .CountAsync(f => f.TenantId == tc.TenantId && f.Status == FollowUpStatus.Pending);

            var openEscalations = await db.Escalations
                .CountAsync(e => e.TenantId == tc.TenantId && e.Status == EscalationStatus.Pending);

            var onlineAgents = await db.AgentPresences
                .Where(p => p.TenantId == tc.TenantId && p.IsOnline)
                .GroupBy(p => p.AgentId)
                .CountAsync();

            var totalLeads = await db.Leads
                .CountAsync(l => l.TenantId == tc.TenantId);

            var newLeadsToday = await db.Leads
                .CountAsync(l => l.TenantId == tc.TenantId && l.CreatedAt >= today);

            var revenueToday = await db.Payments
                .Where(p => p.TenantId == tc.TenantId
                         && p.Status == PaymentStatus.Captured
                         && p.CapturedAt >= today)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            return Results.Ok(new
            {
                callsToday, conversionsToday, openFollowUps, openEscalations,
                onlineAgents, totalLeads, newLeadsToday, revenueToday,
                snapshotAt = DateTime.UtcNow
            });
        });

        // POST /api/live-dashboard/broadcast  — admin pushes a manual refresh to all tenant clients
        group.MapPost("/broadcast", async (TenantContext tc,
            IHubContext<CrmHub> hub) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            await hub.Clients.Group($"tenant-{tc.TenantId}")
                .SendAsync("DashboardUpdated", new { reason = "manual_refresh", ts = DateTime.UtcNow });
            return Results.Ok(new { message = "Broadcast sent." });
        });
    }
}

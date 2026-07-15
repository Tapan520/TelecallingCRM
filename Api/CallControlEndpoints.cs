using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Hubs;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class CallControlEndpoints
{
    public static void MapCallControlEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/calls/{callId:guid}/controls")
            .WithTags("Call Controls")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // POST mute
        group.MapPost("/mute", async (Guid callId, TenantContext tc, AppDbContext db,
            IHubContext<CrmHub> hub, HttpContext http) =>
            await ApplyControlAsync(callId, CallControlAction.Mute, null, tc, db, hub, http));

        // POST unmute
        group.MapPost("/unmute", async (Guid callId, TenantContext tc, AppDbContext db,
            IHubContext<CrmHub> hub, HttpContext http) =>
            await ApplyControlAsync(callId, CallControlAction.Unmute, null, tc, db, hub, http));

        // POST hold
        group.MapPost("/hold", async (Guid callId, TenantContext tc, AppDbContext db,
            IHubContext<CrmHub> hub, HttpContext http) =>
            await ApplyControlAsync(callId, CallControlAction.Hold, null, tc, db, hub, http));

        // POST resume (release hold)
        group.MapPost("/resume", async (Guid callId, TenantContext tc, AppDbContext db,
            IHubContext<CrmHub> hub, HttpContext http) =>
            await ApplyControlAsync(callId, CallControlAction.Resume, null, tc, db, hub, http));

        // POST transfer — requires target party
        group.MapPost("/transfer", async (Guid callId, [FromBody] CallTargetDto dto,
            TenantContext tc, AppDbContext db, IHubContext<CrmHub> hub, HttpContext http) =>
            await ApplyControlAsync(callId, CallControlAction.Transfer, dto.TargetParty, tc, db, hub, http));

        // POST conference — add a third party
        group.MapPost("/conference", async (Guid callId, [FromBody] CallTargetDto dto,
            TenantContext tc, AppDbContext db, IHubContext<CrmHub> hub, HttpContext http) =>
            await ApplyControlAsync(callId, CallControlAction.Conference, dto.TargetParty, tc, db, hub, http));

        // GET control event history for a call
        group.MapGet("/", async (Guid callId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var call = await db.Calls
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == callId && c.TenantId == tc.TenantId);
            if (call == null) return Results.NotFound();

            var events = await db.CallControlEvents
                .Where(e => e.CallId == callId)
                .OrderBy(e => e.OccurredAt)
                .Select(e => new {
                    e.Id, e.Action, e.TargetParty, e.OccurredAt,
                    Agent = e.Agent.FullName
                })
                .ToListAsync();

            return Results.Ok(events);
        });
    }

    private static async Task<IResult> ApplyControlAsync(
        Guid callId, CallControlAction action, string? targetParty,
        TenantContext tc, AppDbContext db, IHubContext<CrmHub> hub, HttpContext http)
    {
        if (!tc.HasTenant) return Results.Unauthorized();

        var call = await db.Calls
            .FirstOrDefaultAsync(c => c.Id == callId && c.TenantId == tc.TenantId);
        if (call == null) return Results.NotFound();

        // Transfer/Conference require a target
        if ((action == CallControlAction.Transfer || action == CallControlAction.Conference)
            && string.IsNullOrWhiteSpace(targetParty))
            return Results.BadRequest("targetParty is required for transfer and conference.");

        var agentId = Guid.Parse(
            http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        var evt = new CallControlEvent
        {
            CallId = callId,
            AgentId = agentId,
            Action = action,
            TargetParty = targetParty,
            OccurredAt = DateTime.UtcNow
        };
        db.CallControlEvents.Add(evt);
        await db.SaveChangesAsync();

        // Broadcast to all agents in the tenant (e.g. supervisor panel)
        await hub.Clients.Group($"tenant-{tc.TenantId}")
            .SendAsync("CallControlApplied", new
            {
                callId,
                action = action.ToString(),
                targetParty,
                agentId,
                occurredAt = evt.OccurredAt
            });

        return Results.Ok(new { evt.Id, evt.Action, evt.OccurredAt });
    }
}

public record CallTargetDto(string TargetParty);

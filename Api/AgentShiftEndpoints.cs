using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
using System.Security.Claims;

namespace TelecallingCRM.Api;

public static class AgentShiftEndpoints
{
    public static void MapAgentShiftEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agent-shifts").WithTags("AgentShifts")
            .RequireAuthorization().RequireRateLimiting("api");

        // GET /api/agent-shifts  — list all shifts for tenant
        group.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var shifts = await db.AgentShifts
                .Where(s => s.TenantId == tc.TenantId)
                .Include(s => s.Agent)
                .OrderBy(s => s.Agent.FullName)
                .Select(s => new
                {
                    s.Id, s.AgentId,
                    AgentName = s.Agent.FullName,
                    s.WorkDays, s.ShiftStartUtc, s.ShiftEndUtc,
                    s.Timezone, s.IsActive, s.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(shifts);
        });

        // GET /api/agent-shifts/available  — agents currently in-shift or online
        group.MapGet("/available", async (TenantContext tc, ILeadAssignmentService svc) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var next = await svc.GetNextAgentAsync(tc.TenantId, null);
            return Results.Ok(new { nextAgentId = next });
        });

        // GET /api/agent-shifts/presence  — latest presence per agent
        group.MapGet("/presence", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var presence = await db.AgentPresences
                .Where(p => p.TenantId == tc.TenantId)
                .Include(p => p.Agent)
                .GroupBy(p => p.AgentId)
                .Select(g => g.OrderByDescending(x => x.ChangedAt).First())
                .Select(p => new
                {
                    p.AgentId,
                    AgentName = p.Agent.FullName,
                    p.IsOnline, p.ChangedAt, p.Note
                })
                .ToListAsync();

            return Results.Ok(presence);
        });

        // POST /api/agent-shifts/presence  — agent sets own online/offline status
        group.MapPost("/presence", async ([FromBody] PresenceDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            db.AgentPresences.Add(new AgentPresence
            {
                TenantId = tc.TenantId,
                AgentId = userId,
                IsOnline = dto.IsOnline,
                Note = dto.Note
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { message = dto.IsOnline ? "Marked online" : "Marked offline" });
        });

        // POST /api/agent-shifts  — create / upsert shift for an agent
        group.MapPost("/", async ([FromBody] UpsertShiftDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var existing = await db.AgentShifts
                .FirstOrDefaultAsync(s => s.TenantId == tc.TenantId && s.AgentId == dto.AgentId);

            if (existing != null)
            {
                existing.WorkDays = dto.WorkDays;
                existing.ShiftStartUtc = dto.ShiftStartUtc;
                existing.ShiftEndUtc = dto.ShiftEndUtc;
                existing.Timezone = dto.Timezone;
                existing.IsActive = dto.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.AgentShifts.Add(new AgentShift
                {
                    TenantId = tc.TenantId,
                    AgentId = dto.AgentId,
                    WorkDays = dto.WorkDays,
                    ShiftStartUtc = dto.ShiftStartUtc,
                    ShiftEndUtc = dto.ShiftEndUtc,
                    Timezone = dto.Timezone,
                    IsActive = dto.IsActive
                });
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Shift saved." });
        });

        // DELETE /api/agent-shifts/{agentId}
        group.MapDelete("/{agentId:guid}", async (Guid agentId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var shift = await db.AgentShifts
                .FirstOrDefaultAsync(s => s.TenantId == tc.TenantId && s.AgentId == agentId);
            if (shift == null) return Results.NotFound();
            db.AgentShifts.Remove(shift);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // POST /api/agent-shifts/round-robin/{leadId}  — manually trigger round-robin
        group.MapPost("/round-robin/{leadId:guid}", async (Guid leadId, TenantContext tc,
            [FromQuery] Guid? campaignId, ILeadAssignmentService svc) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            await svc.AssignRoundRobinAsync(leadId, tc.TenantId, campaignId);
            return Results.Ok(new { message = "Lead assigned via round-robin." });
        });
    }
}

public record PresenceDto(bool IsOnline, string? Note);
public record UpsertShiftDto(Guid AgentId, int WorkDays, TimeSpan ShiftStartUtc, TimeSpan ShiftEndUtc,
    string? Timezone, bool IsActive = true);

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Hubs;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class EscalationEndpoints
{
    public static void MapEscalationEndpoints(this WebApplication app)
    {
        // ?? Escalation Rules (admin only) ??????????????????????????????????
        var rulesGroup = app.MapGroup("/api/escalation-rules")
            .WithTags("Escalations")
            .RequireAuthorization(p => p.RequireRole("admin", "manager", "superadmin"))
            .RequireRateLimiting("api");

        rulesGroup.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var rules = await db.EscalationRules
                .Where(r => r.TenantId == tc.TenantId)
                .Select(r => new {
                    r.Id, r.Name, r.Trigger, r.ThresholdValue,
                    r.IsActive, r.CreatedAt,
                    EscalateTo = r.EscalateTo.FullName
                })
                .ToListAsync();
            return Results.Ok(rules);
        });

        rulesGroup.MapPost("/", async ([FromBody] EscalationRuleDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var rule = new EscalationRule
            {
                TenantId = tc.TenantId,
                Name = dto.Name,
                Trigger = dto.Trigger,
                ThresholdValue = dto.ThresholdValue,
                EscalateToId = dto.EscalateToId,
                IsActive = true
            };
            db.EscalationRules.Add(rule);
            await db.SaveChangesAsync();
            return Results.Created($"/api/escalation-rules/{rule.Id}", new { rule.Id });
        });

        rulesGroup.MapPut("/{id:guid}", async (Guid id, [FromBody] EscalationRuleDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var rule = await db.EscalationRules
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tc.TenantId);
            if (rule == null) return Results.NotFound();
            rule.Name = dto.Name;
            rule.Trigger = dto.Trigger;
            rule.ThresholdValue = dto.ThresholdValue;
            rule.EscalateToId = dto.EscalateToId;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        rulesGroup.MapPost("/{id:guid}/toggle", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var rule = await db.EscalationRules
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tc.TenantId);
            if (rule == null) return Results.NotFound();
            rule.IsActive = !rule.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { rule.IsActive });
        });

        rulesGroup.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var rule = await db.EscalationRules
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tc.TenantId);
            if (rule == null) return Results.NotFound();
            db.EscalationRules.Remove(rule);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ?? Escalation Instances ???????????????????????????????????????????
        var escGroup = app.MapGroup("/api/escalations")
            .WithTags("Escalations")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        escGroup.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var query = db.Escalations
                .Where(e => e.TenantId == tc.TenantId)
                .AsQueryable();

            if (Enum.TryParse<EscalationStatus>(status, true, out var es))
                query = query.Where(e => e.Status == es);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(e => new {
                    e.Id, e.Status, e.Reason, e.CreatedAt, e.AcknowledgedAt, e.ResolvedAt,
                    LeadName = e.Lead.Name, LeadPhone = e.Lead.Phone, e.LeadId,
                    AssignedAgent = e.AssignedAgent.FullName,
                    EscalatedTo = e.EscalatedTo.FullName,
                    RuleName = e.Rule != null ? e.Rule.Name : null
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // Manually raise an escalation
        escGroup.MapPost("/", async ([FromBody] RaiseEscalationDto dto,
            TenantContext tc, AppDbContext db, IHubContext<CrmHub> hub, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var agentId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var escalation = new Escalation
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                AssignedAgentId = agentId,
                EscalatedToId = dto.EscalateToId,
                Reason = dto.Reason,
                RuleId = dto.RuleId
            };
            db.Escalations.Add(escalation);

            db.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tc.TenantId, LeadId = dto.LeadId, UserId = agentId,
                Type = ActivityType.EscalationRaised,
                Summary = $"Escalation raised: {dto.Reason}"
            });

            db.Notifications.Add(new Notification
            {
                TenantId = tc.TenantId, UserId = dto.EscalateToId,
                Type = NotificationType.SystemAlert,
                Title = "New Escalation",
                Body = dto.Reason,
                Link = $"/Leads/Timeline/{dto.LeadId}"
            });

            await db.SaveChangesAsync();

            await hub.Clients.User(dto.EscalateToId.ToString())
                .SendAsync("EscalationRaised", new { escalation.Id, dto.LeadId, dto.Reason });

            return Results.Created($"/api/escalations/{escalation.Id}", new { escalation.Id });
        });

        // Acknowledge escalation
        escGroup.MapPost("/{id:guid}/acknowledge", async (Guid id,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var esc = await db.Escalations
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tc.TenantId);
            if (esc == null) return Results.NotFound();
            esc.Status = EscalationStatus.Acknowledged;
            esc.AcknowledgedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { esc.Status, esc.AcknowledgedAt });
        });

        // Resolve escalation
        escGroup.MapPost("/{id:guid}/resolve", async (Guid id, [FromBody] ResolveEscalationDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var esc = await db.Escalations
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tc.TenantId);
            if (esc == null) return Results.NotFound();
            var userId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            esc.Status = EscalationStatus.Resolved;
            esc.ResolvedAt = DateTime.UtcNow;
            esc.ResolutionNote = dto.Note;

            db.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tc.TenantId, LeadId = esc.LeadId, UserId = userId,
                Type = ActivityType.EscalationResolved,
                Summary = $"Escalation resolved: {dto.Note}"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { esc.Status, esc.ResolvedAt });
        });

        // Dismiss escalation
        escGroup.MapPost("/{id:guid}/dismiss", async (Guid id,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var esc = await db.Escalations
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tc.TenantId);
            if (esc == null) return Results.NotFound();
            esc.Status = EscalationStatus.Dismissed;
            await db.SaveChangesAsync();
            return Results.Ok(new { esc.Status });
        });
    }
}

public record EscalationRuleDto(
    string Name, EscalationTrigger Trigger, int ThresholdValue, Guid EscalateToId);

public record RaiseEscalationDto(Guid LeadId, Guid EscalateToId, string Reason, Guid? RuleId);

public record ResolveEscalationDto(string? Note);

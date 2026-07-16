using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class CommissionEndpoints
{
    public static void MapCommissionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/commissions").WithTags("Commissions").RequireAuthorization().RequireRateLimiting("api");

        // ?? Rules ????????????????????????????????????????????????????????????
        group.MapGet("/rules", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var rules = await db.CommissionRules
                .Where(r => r.TenantId == tc.TenantId)
                .Select(r => new { r.Id, r.Name, r.Type, r.Value, r.IsActive, r.CampaignId,
                    CampaignName = r.Campaign != null ? r.Campaign.Name : null })
                .ToListAsync();
            return Results.Ok(rules);
        });

        group.MapPost("/rules", async ([FromBody] CommissionRuleDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var rule = new CommissionRule
            {
                TenantId = tc.TenantId,
                Name = dto.Name, Type = dto.Type, Value = dto.Value,
                CampaignId = dto.CampaignId, IsActive = dto.IsActive
            };
            db.CommissionRules.Add(rule);
            await db.SaveChangesAsync();
            return Results.Created($"/api/commissions/rules/{rule.Id}", new { rule.Id, rule.Name });
        });

        group.MapPut("/rules/{id:guid}", async (Guid id, [FromBody] CommissionRuleDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var rule = await db.CommissionRules.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tc.TenantId);
            if (rule is null) return Results.NotFound();
            rule.Name = dto.Name; rule.Type = dto.Type; rule.Value = dto.Value;
            rule.CampaignId = dto.CampaignId; rule.IsActive = dto.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { rule.Id });
        });

        group.MapDelete("/rules/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var rule = await db.CommissionRules.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tc.TenantId);
            if (rule is null) return Results.NotFound();
            db.CommissionRules.Remove(rule);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ?? Entries ???????????????????????????????????????????????????????????
        group.MapGet("/entries", async (TenantContext tc, AppDbContext db,
            [FromQuery] Guid? agentId, [FromQuery] string? status,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var q = db.CommissionEntries.Where(e => e.TenantId == tc.TenantId);
            if (agentId.HasValue) q = q.Where(e => e.AgentId == agentId.Value);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CommissionStatus>(status, true, out var cs))
                q = q.Where(e => e.Status == cs);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(e => e.EarnedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(e => new {
                    e.Id, e.Amount, e.Status, e.EarnedAt, e.PaidAt, e.Note,
                    AgentName = e.Agent.FullName, e.AgentId,
                    LeadName = e.Lead != null ? e.Lead.Name : null
                }).ToListAsync();
            return Results.Ok(new { total, page, pageSize, items });
        });

        group.MapPost("/entries", async ([FromBody] CommissionEntryDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var entry = new CommissionEntry
            {
                TenantId = tc.TenantId,
                AgentId = dto.AgentId, PaymentId = dto.PaymentId,
                LeadId = dto.LeadId, RuleId = dto.RuleId,
                Amount = dto.Amount, Note = dto.Note
            };
            db.CommissionEntries.Add(entry);
            await db.SaveChangesAsync();
            return Results.Created($"/api/commissions/entries/{entry.Id}", new { entry.Id, entry.Amount });
        });

        // PATCH /api/commissions/entries/{id}/approve
        group.MapPatch("/entries/{id:guid}/approve", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var entry = await db.CommissionEntries.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tc.TenantId);
            if (entry is null) return Results.NotFound();
            entry.Status = CommissionStatus.Approved;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // PATCH /api/commissions/entries/{id}/mark-paid
        group.MapPatch("/entries/{id:guid}/mark-paid", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var entry = await db.CommissionEntries.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tc.TenantId);
            if (entry is null) return Results.NotFound();
            entry.Status = CommissionStatus.Paid;
            entry.PaidAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // GET /api/commissions/summary — per-agent totals
        group.MapGet("/summary", async (TenantContext tc, AppDbContext db,
            [FromQuery] int month = 0, [FromQuery] int year = 0) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var m = month > 0 ? month : DateTime.UtcNow.Month;
            var y = year > 0 ? year : DateTime.UtcNow.Year;
            var from = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = from.AddMonths(1);
            var data = await db.CommissionEntries
                .Where(e => e.TenantId == tc.TenantId && e.EarnedAt >= from && e.EarnedAt < to)
                .GroupBy(e => e.AgentId)
                .Select(g => new {
                    AgentId = g.Key,
                    TotalEarned = g.Sum(e => e.Amount),
                    Pending = g.Where(e => e.Status == CommissionStatus.Pending).Sum(e => e.Amount),
                    Approved = g.Where(e => e.Status == CommissionStatus.Approved).Sum(e => e.Amount),
                    Paid = g.Where(e => e.Status == CommissionStatus.Paid).Sum(e => e.Amount)
                })
                .Join(db.Users, a => a.AgentId, u => u.Id, (a, u) => new {
                    u.FullName, a.AgentId, a.TotalEarned, a.Pending, a.Approved, a.Paid
                })
                .OrderByDescending(x => x.TotalEarned)
                .ToListAsync();
            return Results.Ok(new { month = m, year = y, agents = data });
        });
    }
}

public record CommissionRuleDto(string Name, CommissionType Type, decimal Value, bool IsActive, Guid? CampaignId);
public record CommissionEntryDto(Guid AgentId, decimal Amount, Guid? PaymentId, Guid? LeadId, Guid? RuleId, string? Note);

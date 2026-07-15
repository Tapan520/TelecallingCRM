using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class AiInsightsEndpoints
{
    public static void MapAiInsightsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ai/insights").WithTags("AI Insights").RequireAuthorization().RequireRateLimiting("api");

        // High-probability conversion candidates
        group.MapGet("/hot-leads", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId
                         && l.Status != LeadStatus.Converted
                         && l.Status != LeadStatus.Dead
                         && l.AiScore >= 60)
                .Include(l => l.AssignedTo)
                .OrderByDescending(l => l.AiScore)
                .Take(25)
                .Select(l => new {
                    l.Id, l.Name, l.Phone, l.Status, l.AiScore, l.AiInsight,
                    l.NextFollowUpAt, l.LastContactedAt,
                    AssignedTo = l.AssignedTo != null ? l.AssignedTo.FullName : null
                })
                .ToListAsync();
            return Results.Ok(new { count = leads.Count, leads });
        });

        // Leads due for follow-up today
        group.MapGet("/call-today", async (TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId
                         && l.AssignedToId == userId
                         && l.NextFollowUpAt >= today && l.NextFollowUpAt < tomorrow
                         && l.Status != LeadStatus.Converted && l.Status != LeadStatus.Dead)
                .Select(l => new { l.Id, l.Name, l.Phone, l.Status, l.NextFollowUpAt, l.AiScore })
                .OrderBy(l => l.NextFollowUpAt)
                .ToListAsync();
            return Results.Ok(new { count = leads.Count, leads });
        });

        // Unhappy customers (negative sentiment calls in last 7 days)
        group.MapGet("/unhappy-customers", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var since = DateTime.UtcNow.AddDays(-7);
            var leads = await db.Calls
                .Where(c => c.TenantId == tc.TenantId && c.StartedAt >= since && c.AiSentiment == "negative")
                .Include(c => c.Lead)
                .GroupBy(c => c.LeadId)
                .Select(g => new {
                    LeadId = g.Key,
                    NegativeCallCount = g.Count(),
                    LastNegativeCall = g.Max(c => c.StartedAt),
                    LeadName = g.First().Lead.Name,
                    Phone = g.First().Lead.Phone,
                    Insight = g.First().AiInsight
                })
                .OrderByDescending(x => x.NegativeCallCount)
                .Take(20)
                .ToListAsync();
            return Results.Ok(new { count = leads.Count, leads });
        });

        // Competitor mentions
        group.MapGet("/competitor-mentions", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var calls = await db.Calls
                .Where(c => c.TenantId == tc.TenantId
                         && c.AiInsight != null && c.AiInsight.Contains("competitor"))
                .Include(c => c.Lead).Include(c => c.Agent)
                .OrderByDescending(c => c.StartedAt)
                .Take(50)
                .Select(c => new {
                    c.Id, c.StartedAt, c.AiInsight, c.AiSummary,
                    LeadName = c.Lead.Name, LeadPhone = c.Lead.Phone, c.LeadId,
                    Agent = c.Agent.FullName
                })
                .ToListAsync();
            return Results.Ok(new { count = calls.Count, calls });
        });

        // Recalculate AI scores for all active leads (batch)
        group.MapPost("/recalculate-scores", async (TenantContext tc, AppDbContext db, IOpenRouterService ai) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tenant = await db.Tenants.FindAsync(tc.TenantId);
            if (tenant == null) return Results.Unauthorized();

            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && l.Status != LeadStatus.Converted && l.Status != LeadStatus.Dead)
                .Include(l => l.Calls.OrderByDescending(c => c.StartedAt).Take(3))
                .Take(50) // batch limit
                .ToListAsync();

            foreach (var lead in leads)
            {
                var callSummaries = lead.Calls.Select(c => $"Outcome:{c.Outcome} Sentiment:{c.AiSentiment}").ToList();
                var prompt = $"Rate this lead's conversion probability 0-100. Lead: {lead.Name}, Status: {lead.Status}, " +
                             $"Priority: {lead.Priority}, Calls: {callSummaries.Count}, " +
                             $"Recent calls: {string.Join("; ", callSummaries)}. " +
                             $"Reply with ONLY a number 0-100.";
                var scoreStr = await ai.ChatAsync(prompt, null, tenant);
                if (int.TryParse(scoreStr.Trim().Split(' ')[0], out var score))
                    lead.AiScore = Math.Clamp(score, 0, 100);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { updated = leads.Count });
        });

        // GET AI-ranked lead list sorted by conversion probability
        group.MapGet("/ranked-leads", async (TenantContext tc, AppDbContext db,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var total = await db.Leads.CountAsync(l => l.TenantId == tc.TenantId
                && l.Status != LeadStatus.Converted && l.Status != LeadStatus.Dead);
            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId
                         && l.Status != LeadStatus.Converted
                         && l.Status != LeadStatus.Dead)
                .OrderByDescending(l => l.AiScore)
                .ThenByDescending(l => l.Priority)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new {
                    l.Id, l.Name, l.Phone, l.Status, l.AiScore, l.AiInsight,
                    l.Priority, l.NextFollowUpAt, l.LastContactedAt,
                    AssignedTo = l.AssignedTo != null ? l.AssignedTo.FullName : null
                })
                .ToListAsync();
            return Results.Ok(new { total, page, pageSize, leads });
        });

        // POST call coaching — AI feedback on a specific call transcript
        group.MapPost("/call-coaching/{callId:guid}", async (Guid callId, TenantContext tc,
            AppDbContext db, IOpenRouterService ai) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var call = await db.Calls.FirstOrDefaultAsync(c => c.Id == callId && c.TenantId == tc.TenantId);
            if (call == null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(call.TranscriptText))
                return Results.BadRequest("No transcript available for this call.");

            var tenant = await db.Tenants.FindAsync(tc.TenantId);
            if (tenant == null) return Results.Unauthorized();

            var systemPrompt = "You are a telecalling coach. Analyse the following sales call transcript and provide structured feedback on: " +
                               "1) Opening (first impression), 2) Objection handling, 3) Tone and empathy, " +
                               "4) Closing technique, 5) One key improvement suggestion. Be concise and actionable.";
            var coaching = await ai.ChatAsync(call.TranscriptText, systemPrompt, tenant);
            return Results.Ok(new { callId, coaching });
        });
    }
}


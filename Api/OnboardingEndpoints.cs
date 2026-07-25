using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class OnboardingEndpoints
{
    private static readonly List<(string Step, string Description, int Order)> DefaultSteps = new()
    {
        ("Complete Profile",    "Fill in your full name, phone and profile picture",  1),
        ("Read Company Policy", "Read and acknowledge the company guidelines",         2),
        ("Setup CRM Access",    "Log in and explore the CRM dashboard",               3),
        ("First Call",          "Make your first call using the dialer",              4),
        ("Knowledge Base",      "Read at least 3 knowledge base articles",            5),
        ("Attend Team Meeting", "Join your first team standup meeting",               6),
    };

    public static void MapOnboardingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/onboarding")
            .WithTags("Onboarding")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET checklist for self or agent
        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";
            var targetId = (agentId.HasValue && (role == "admin" || role == "manager")) ? agentId.Value : callerId;

            var items = await db.OnboardingChecklists
                .Where(o => o.TenantId == tc.TenantId && o.AgentId == targetId)
                .OrderBy(o => o.StepOrder)
                .Select(o => new { o.Id, o.StepName, o.Description, o.StepOrder, o.Status, o.CompletedAt })
                .ToListAsync();

            return Results.Ok(items);
        });

        // POST initialise checklist for a new agent (admin triggers this)
        group.MapPost("/init/{agentId:guid}", async (Guid agentId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var existing = await db.OnboardingChecklists
                .AnyAsync(o => o.TenantId == tc.TenantId && o.AgentId == agentId);
            if (existing) return Results.BadRequest("Checklist already exists.");

            var steps = DefaultSteps.Select(s => new OnboardingChecklist
            {
                TenantId = tc.TenantId, AgentId = agentId,
                StepName = s.Step, Description = s.Description, StepOrder = s.Order
            }).ToList();

            db.OnboardingChecklists.AddRange(steps);
            await db.SaveChangesAsync();
            return Results.Ok(new { created = steps.Count });
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));

        // PUT mark step complete
        group.MapPut("/{id:guid}/complete", async (Guid id, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var step = await db.OnboardingChecklists
                .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tc.TenantId);
            if (step == null) return Results.NotFound();
            if (step.AgentId != callerId) return Results.Forbid();

            step.Status = OnboardingStepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // GET all agents onboarding progress (admin/manager)
        group.MapGet("/progress", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var progress = await db.OnboardingChecklists
                .Where(o => o.TenantId == tc.TenantId)
                .GroupBy(o => new { o.AgentId })
                .Select(g => new {
                    g.Key.AgentId,
                    AgentName   = g.First().Agent.FullName,
                    Total       = g.Count(),
                    Completed   = g.Count(o => o.Status == OnboardingStepStatus.Completed),
                    Pending     = g.Count(o => o.Status == OnboardingStepStatus.Pending)
                })
                .ToListAsync();

            return Results.Ok(progress);
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));
    }
}

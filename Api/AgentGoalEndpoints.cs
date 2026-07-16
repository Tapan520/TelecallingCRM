using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
using System.Security.Claims;

namespace TelecallingCRM.Api;

/// <summary>
/// Agent Goals &amp; Daily Targets management.
///
/// Admin/Manager:
///   GET    /api/goals                        – list all goals for tenant
///   POST   /api/goals                        – create a goal for an agent
///   PUT    /api/goals/{id}                   – update goal targets
///   DELETE /api/goals/{id}                   – delete a goal
///   GET    /api/goals/{id}/progress          – goal + actual vs. target metrics
///
/// Agent (any authenticated user):
///   GET    /api/goals/my                     – active goals for the calling agent
///   GET    /api/goals/my/today               – today's metrics vs. today's active goal
/// </summary>
public static class AgentGoalEndpoints
{
    public static void MapAgentGoalEndpoints(this WebApplication app)
    {
        // ?? Admin / Manager group ?????????????????????????????????????????
        var admin = app.MapGroup("/api/goals")
            .WithTags("Agent Goals")
            .RequireAuthorization(p => p.RequireRole("admin", "manager", "superadmin"))
            .RequireRateLimiting("api");

        // LIST all goals for tenant
        admin.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] Guid? agentId, [FromQuery] bool? active) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var q = db.AgentGoals.Where(g => g.TenantId == tc.TenantId).AsQueryable();
            if (agentId.HasValue) q = q.Where(g => g.AgentId == agentId.Value);
            if (active.HasValue) q = q.Where(g => g.IsActive == active.Value);

            var goals = await q
                .OrderByDescending(g => g.PeriodStart)
                .Select(g => new {
                    g.Id, g.Label, g.AgentId,
                    AgentName   = g.Agent.FullName,
                    g.PeriodStart, g.PeriodEnd, g.IsActive,
                    g.TargetCalls, g.TargetConversions,
                    g.TargetTalkSeconds, g.TargetFollowUps,
                    g.CreatedAt
                })
                .ToListAsync();
            return Results.Ok(goals);
        });

        // CREATE goal
        admin.MapPost("/", async ([FromBody] GoalUpsertDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var creatorId = Guid.Parse(
                http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var goal = new AgentGoal
            {
                TenantId          = tc.TenantId,
                AgentId           = dto.AgentId,
                CreatedById       = creatorId,
                Label             = dto.Label,
                TargetCalls       = dto.TargetCalls,
                TargetConversions = dto.TargetConversions,
                TargetTalkSeconds = dto.TargetTalkSeconds,
                TargetFollowUps   = dto.TargetFollowUps,
                PeriodStart       = dto.PeriodStart.ToUniversalTime(),
                PeriodEnd         = dto.PeriodEnd.ToUniversalTime()
            };
            db.AgentGoals.Add(goal);
            await db.SaveChangesAsync();
            return Results.Created($"/api/goals/{goal.Id}", new { goal.Id });
        });

        // UPDATE goal
        admin.MapPut("/{id:guid}", async (Guid id, [FromBody] GoalUpsertDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var goal = await db.AgentGoals
                .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tc.TenantId);
            if (goal == null) return Results.NotFound();

            goal.Label             = dto.Label;
            goal.TargetCalls       = dto.TargetCalls;
            goal.TargetConversions = dto.TargetConversions;
            goal.TargetTalkSeconds = dto.TargetTalkSeconds;
            goal.TargetFollowUps   = dto.TargetFollowUps;
            goal.PeriodStart       = dto.PeriodStart.ToUniversalTime();
            goal.PeriodEnd         = dto.PeriodEnd.ToUniversalTime();
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // TOGGLE active
        admin.MapPost("/{id:guid}/toggle", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var goal = await db.AgentGoals
                .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tc.TenantId);
            if (goal == null) return Results.NotFound();
            goal.IsActive = !goal.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { goal.IsActive });
        });

        // DELETE
        admin.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var goal = await db.AgentGoals
                .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tc.TenantId);
            if (goal == null) return Results.NotFound();
            db.AgentGoals.Remove(goal);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // GET progress for a specific goal (actual vs. target)
        admin.MapGet("/{id:guid}/progress", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var goal = await db.AgentGoals
                .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tc.TenantId);
            if (goal == null) return Results.NotFound();
            var progress = await ComputeProgressAsync(goal, db);
            return Results.Ok(progress);
        });

        // ?? Agent self-service group ??????????????????????????????????????
        var agent = app.MapGroup("/api/goals/my")
            .WithTags("Agent Goals")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // Active goals for the calling agent
        agent.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(
                http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var now = DateTime.UtcNow;

            var goals = await db.AgentGoals
                .Where(g => g.TenantId == tc.TenantId
                         && g.AgentId  == userId
                         && g.IsActive
                         && g.PeriodStart <= now
                         && g.PeriodEnd   >= now)
                .OrderByDescending(g => g.PeriodStart)
                .ToListAsync();

            var results = new List<object>();
            foreach (var goal in goals)
                results.Add(await ComputeProgressAsync(goal, db));

            return Results.Ok(results);
        });

        // Today's metrics snapshot vs. the active goal that covers today
        agent.MapGet("/today", async (TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(
                http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var today     = DateTime.UtcNow.Date;
            var tomorrow  = today.AddDays(1);

            // Calls made today
            var todayCalls = await db.Calls
                .CountAsync(c => c.TenantId == tc.TenantId
                              && c.AgentId  == userId
                              && c.StartedAt >= today
                              && c.StartedAt <  tomorrow);

            var todayTalkSeconds = await db.Calls
                .Where(c => c.TenantId == tc.TenantId
                         && c.AgentId  == userId
                         && c.StartedAt >= today
                         && c.StartedAt <  tomorrow)
                .SumAsync(c => (int?)c.DurationSeconds) ?? 0;

            var todayConversions = await db.Calls
                .CountAsync(c => c.TenantId == tc.TenantId
                              && c.AgentId  == userId
                              && c.StartedAt >= today
                              && c.StartedAt <  tomorrow
                              && c.Outcome   == CallOutcome.Converted);

            var todayFollowUps = await db.FollowUps
                .CountAsync(f => f.TenantId    == tc.TenantId
                              && f.AssignedToId == userId
                              && f.Status       == FollowUpStatus.Done
                              && f.ScheduledAt  >= today
                              && f.ScheduledAt  <  tomorrow);

            // Find an active goal that covers today
            var goal = await db.AgentGoals
                .Where(g => g.TenantId    == tc.TenantId
                         && g.AgentId     == userId
                         && g.IsActive
                         && g.PeriodStart <= today
                         && g.PeriodEnd   >= today)
                .OrderByDescending(g => g.PeriodStart)
                .FirstOrDefaultAsync();

            return Results.Ok(new
            {
                date             = today,
                actualCalls      = todayCalls,
                actualTalkSeconds= todayTalkSeconds,
                actualConversions= todayConversions,
                actualFollowUps  = todayFollowUps,
                goal = goal == null ? null : new
                {
                    goal.Id, goal.Label,
                    goal.TargetCalls, goal.TargetConversions,
                    goal.TargetTalkSeconds, goal.TargetFollowUps
                },
                // Percentage progress (capped at 100 for display)
                callsPct       = goal?.TargetCalls       > 0 ? Math.Min(100, (int)((double)todayCalls       / goal.TargetCalls       * 100)) : (int?)null,
                conversionsPct = goal?.TargetConversions > 0 ? Math.Min(100, (int)((double)todayConversions / goal.TargetConversions * 100)) : (int?)null,
                talkPct        = goal?.TargetTalkSeconds > 0 ? Math.Min(100, (int)((double)todayTalkSeconds / goal.TargetTalkSeconds * 100)) : (int?)null,
                followUpsPct   = goal?.TargetFollowUps   > 0 ? Math.Min(100, (int)((double)todayFollowUps   / goal.TargetFollowUps   * 100)) : (int?)null
            });
        });
    }

    // ?? Shared progress calculator ????????????????????????????????????????????
    private static async Task<object> ComputeProgressAsync(AgentGoal goal, AppDbContext db)
    {
        var start = goal.PeriodStart;
        var end   = goal.PeriodEnd;

        var actualCalls = await db.Calls
            .CountAsync(c => c.AgentId   == goal.AgentId
                          && c.TenantId  == goal.TenantId
                          && c.StartedAt >= start
                          && c.StartedAt <= end);

        var actualTalkSeconds = await db.Calls
            .Where(c => c.AgentId   == goal.AgentId
                     && c.TenantId  == goal.TenantId
                     && c.StartedAt >= start
                     && c.StartedAt <= end)
            .SumAsync(c => (int?)c.DurationSeconds) ?? 0;

        var actualConversions = await db.Calls
            .CountAsync(c => c.AgentId   == goal.AgentId
                          && c.TenantId  == goal.TenantId
                          && c.StartedAt >= start
                          && c.StartedAt <= end
                          && c.Outcome   == CallOutcome.Converted);

        var actualFollowUps = await db.FollowUps
            .CountAsync(f => f.AssignedToId == goal.AgentId
                          && f.TenantId     == goal.TenantId
                          && f.Status       == FollowUpStatus.Done
                          && f.ScheduledAt  >= start
                          && f.ScheduledAt  <= end);

        return new
        {
            goal.Id, goal.Label, goal.AgentId,
            goal.PeriodStart, goal.PeriodEnd, goal.IsActive,
            targets = new {
                goal.TargetCalls, goal.TargetConversions,
                goal.TargetTalkSeconds, goal.TargetFollowUps
            },
            actuals = new {
                actualCalls, actualConversions,
                actualTalkSeconds, actualFollowUps
            },
            progress = new {
                callsPct        = goal.TargetCalls       > 0 ? Math.Min(100, (int)((double)actualCalls       / goal.TargetCalls       * 100)) : (int?)null,
                conversionsPct  = goal.TargetConversions > 0 ? Math.Min(100, (int)((double)actualConversions / goal.TargetConversions * 100)) : (int?)null,
                talkPct         = goal.TargetTalkSeconds > 0 ? Math.Min(100, (int)((double)actualTalkSeconds / goal.TargetTalkSeconds * 100)) : (int?)null,
                followUpsPct    = goal.TargetFollowUps   > 0 ? Math.Min(100, (int)((double)actualFollowUps   / goal.TargetFollowUps   * 100)) : (int?)null
            }
        };
    }
}

public record GoalUpsertDto(
    Guid AgentId,
    string Label,
    int TargetCalls,
    int TargetConversions,
    int TargetTalkSeconds,
    int TargetFollowUps,
    DateTime PeriodStart,
    DateTime PeriodEnd);

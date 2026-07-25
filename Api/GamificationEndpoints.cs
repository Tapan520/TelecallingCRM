using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class GamificationEndpoints
{
    public static void MapGamificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gamification")
            .WithTags("Gamification")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET leaderboard with points
        group.MapGet("/leaderboard", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? period) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var badgePoints = new Dictionary<BadgeType, int>
            {
                { BadgeType.FirstCall, 10 }, { BadgeType.HundredCalls, 50 },
                { BadgeType.FirstSale, 25 }, { BadgeType.TopPerformerWeek, 100 },
                { BadgeType.TopPerformerMonth, 200 }, { BadgeType.PerfectAttendance, 75 },
                { BadgeType.FastResponder, 30 }, { BadgeType.LeadConvertor, 40 }
            };

            var badges = await db.AgentBadges
                .Where(b => b.TenantId == tc.TenantId)
                .Select(b => new { b.AgentId, AgentName = b.Agent.FullName, b.Badge, b.Points, b.EarnedAt })
                .ToListAsync();

            var leaderboard = badges
                .GroupBy(b => new { b.AgentId, b.AgentName })
                .Select(g => new {
                    g.Key.AgentId, g.Key.AgentName,
                    TotalPoints  = g.Sum(b => b.Points),
                    TotalBadges  = g.Count(),
                    Badges       = g.Select(b => b.Badge.ToString()).Distinct()
                })
                .OrderByDescending(x => x.TotalPoints)
                .ToList();

            return Results.Ok(leaderboard);
        });

        // GET badges for current user or specific agent
        group.MapGet("/badges", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var targetId = agentId ?? callerId;

            var badges = await db.AgentBadges
                .Where(b => b.TenantId == tc.TenantId && b.AgentId == targetId)
                .OrderByDescending(b => b.EarnedAt)
                .Select(b => new { b.Id, b.Badge, b.Points, b.Notes, b.EarnedAt })
                .ToListAsync();

            return Results.Ok(badges);
        });

        // POST award badge (admin/manager)
        group.MapPost("/award", async ([FromBody] AwardBadgeDto dto, TenantContext tc,
            AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var pointsMap = new Dictionary<BadgeType, int>
            {
                { BadgeType.FirstCall, 10 }, { BadgeType.HundredCalls, 50 },
                { BadgeType.FirstSale, 25 }, { BadgeType.TopPerformerWeek, 100 },
                { BadgeType.TopPerformerMonth, 200 }, { BadgeType.PerfectAttendance, 75 },
                { BadgeType.FastResponder, 30 }, { BadgeType.LeadConvertor, 40 }
            };

            var badge = new AgentBadge
            {
                TenantId = tc.TenantId, AgentId = dto.AgentId,
                Badge = dto.Badge, Notes = dto.Notes,
                Points = pointsMap.TryGetValue(dto.Badge, out var pts) ? pts : 10
            };
            db.AgentBadges.Add(badge);
            await db.SaveChangesAsync();
            return Results.Created($"/api/gamification/badges/{badge.Id}", new { badge.Id, badge.Points });
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));
    }
}

public record AwardBadgeDto(Guid AgentId, BadgeType Badge, string? Notes);

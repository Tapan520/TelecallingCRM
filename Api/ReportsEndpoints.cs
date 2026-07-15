using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization().RequireRateLimiting("api");

        // Lead source breakdown
        group.MapGet("/lead-sources", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var data = await db.Leads
                .Where(l => l.TenantId == tc.TenantId)
                .GroupBy(l => l.Source ?? "Unknown")
                .Select(g => new { Source = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
            return Results.Ok(data);
        });

        // Conversion funnel
        group.MapGet("/conversion-funnel", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var data = await db.Leads
                .Where(l => l.TenantId == tc.TenantId)
                .GroupBy(l => l.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();
            return Results.Ok(data);
        });

        // Per-agent performance
        group.MapGet("/agent-performance", async (TenantContext tc, AppDbContext db,
            [FromQuery] int days = 30) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var since = DateTime.UtcNow.AddDays(-days);
            var data = await db.Calls
                .Where(c => c.TenantId == tc.TenantId && c.StartedAt >= since)
                .GroupBy(c => c.AgentId)
                .Select(g => new {
                    AgentId = g.Key,
                    TotalCalls = g.Count(),
                    Connected = g.Count(c => c.DurationSeconds > 10),
                    TotalTalkSeconds = g.Sum(c => c.DurationSeconds),
                    Converted = g.Count(c => c.Outcome == CallOutcome.Converted),
                    AvgDuration = g.Average(c => (double)c.DurationSeconds)
                })
                .Join(db.Users, a => a.AgentId, u => u.Id, (a, u) => new {
                    u.FullName, a.TotalCalls, a.Connected, a.TotalTalkSeconds,
                    a.Converted, AvgDuration = (int)a.AvgDuration,
                    ConversionRate = a.TotalCalls > 0 ? Math.Round((double)a.Converted / a.TotalCalls * 100, 1) : 0
                })
                .OrderByDescending(x => x.TotalCalls)
                .ToListAsync();
            return Results.Ok(data);
        });

        // Daily calls for the last N days
        group.MapGet("/daily-calls", async (TenantContext tc, AppDbContext db,
            [FromQuery] int days = 14) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var since = DateTime.UtcNow.AddDays(-days);
            var data = await db.Calls
                .Where(c => c.TenantId == tc.TenantId && c.StartedAt >= since)
                .GroupBy(c => c.StartedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count(), TalkSeconds = g.Sum(c => c.DurationSeconds) })
                .OrderBy(x => x.Date)
                .ToListAsync();
            return Results.Ok(data);
        });

        // Campaign performance
        group.MapGet("/campaign-performance", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var data = await db.Campaigns
                .Where(c => c.TenantId == tc.TenantId)
                .Select(c => new {
                    c.Id, c.Name, c.Status, c.Type,
                    TotalLeads = c.Leads.Count,
                    Converted = c.Leads.Count(l => l.Status == LeadStatus.Converted),
                    Interested = c.Leads.Count(l => l.Status == LeadStatus.Interested),
                    TotalCalls = c.Leads.SelectMany(l => l.Calls).Count()
                })
                .ToListAsync();
            return Results.Ok(data);
        });

        // Missed / no-answer calls
        group.MapGet("/missed-calls", async (TenantContext tc, AppDbContext db,
            [FromQuery] int days = 7) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var since = DateTime.UtcNow.AddDays(-days);
            var data = await db.Calls
                .Where(c => c.TenantId == tc.TenantId && c.StartedAt >= since
                         && (c.Outcome == CallOutcome.NoAnswer || c.Outcome == CallOutcome.SwitchOff))
                .Include(c => c.Lead).Include(c => c.Agent)
                .OrderByDescending(c => c.StartedAt)
                .Select(c => new {
                    c.Id, c.StartedAt, c.Outcome,
                    LeadName = c.Lead.Name, LeadPhone = c.Lead.Phone, c.LeadId,
                    Agent = c.Agent.FullName
                })
                .Take(100)
                .ToListAsync();
            return Results.Ok(data);
        });

        // Summary stats for Reports page
        group.MapGet("/summary", async (TenantContext tc, AppDbContext db,
            [FromQuery] int days = 30) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tid = tc.TenantId;
            var since = DateTime.UtcNow.AddDays(-days);
            return Results.Ok(new {
                totalLeads = await db.Leads.CountAsync(l => l.TenantId == tid),
                newLeads = await db.Leads.CountAsync(l => l.TenantId == tid && l.CreatedAt >= since),
                converted = await db.Leads.CountAsync(l => l.TenantId == tid && l.Status == LeadStatus.Converted),
                totalCalls = await db.Calls.CountAsync(c => c.TenantId == tid && c.StartedAt >= since),
                totalTalkSeconds = await db.Calls.Where(c => c.TenantId == tid && c.StartedAt >= since).SumAsync(c => (long)c.DurationSeconds),
                pendingFollowUps = await db.FollowUps.CountAsync(f => f.TenantId == tid && f.Status == FollowUpStatus.Pending),
                overdueTasks = await db.Tasks.CountAsync(t => t.TenantId == tid && t.Status == TelecallingCRM.Data.Models.TaskStatus.Overdue),
                activeCampaigns = await db.Campaigns.CountAsync(c => c.TenantId == tid && c.Status == CampaignStatus.Active)
            });
        });

        // Weekly conversion rate trend over last N days
        group.MapGet("/conversion-trend", async (TenantContext tc, AppDbContext db,
            [FromQuery] int days = 90) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var since = DateTime.UtcNow.AddDays(-days);
            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && l.CreatedAt >= since)
                .Select(l => new { l.CreatedAt, l.Status })
                .ToListAsync();

            var grouped = leads
                .GroupBy(l => System.Globalization.ISOWeek.GetWeekOfYear(l.CreatedAt))
                .OrderBy(g => g.Key)
                .Select(g => new {
                    Week = g.Key,
                    Total = g.Count(),
                    Converted = g.Count(l => l.Status == LeadStatus.Converted),
                    ConversionRate = g.Count() > 0
                        ? Math.Round((double)g.Count(l => l.Status == LeadStatus.Converted) / g.Count() * 100, 1)
                        : 0.0
                });
            return Results.Ok(grouped);
        });

        // Hourly call heatmap (hour-of-day vs count, for optimal call timing)
        group.MapGet("/hourly-heatmap", async (TenantContext tc, AppDbContext db,
            [FromQuery] int days = 30) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var since = DateTime.UtcNow.AddDays(-days);
            var data = await db.Calls
                .Where(c => c.TenantId == tc.TenantId && c.StartedAt >= since)
                .Select(c => new { Hour = c.StartedAt.Hour, c.Outcome, c.DurationSeconds })
                .ToListAsync();

            var heatmap = data
                .GroupBy(c => c.Hour)
                .OrderBy(g => g.Key)
                .Select(g => new {
                    Hour = g.Key,
                    TotalCalls = g.Count(),
                    Connected = g.Count(c => c.DurationSeconds > 10),
                    Converted = g.Count(c => c.Outcome == CallOutcome.Converted)
                });
            return Results.Ok(heatmap);
        });

        // SuperAdmin tenant usage overview
        group.MapGet("/tenant-usage", async (AppDbContext db, HttpContext http) =>
        {
            var role = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role != "superadmin") return Results.Forbid();

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var data = await db.Tenants
                .Select(t => new {
                    t.Id, t.Name, t.Slug, t.Plan, t.IsActive,
                    LeadCount = db.Leads.Count(l => l.TenantId == t.Id),
                    UserCount = db.Users.Count(u => u.TenantId == t.Id),
                    CallsThisMonth = db.Calls.Count(c => c.TenantId == t.Id && c.StartedAt >= monthStart),
                    LastActive = db.Calls
                        .Where(c => c.TenantId == t.Id)
                        .OrderByDescending(c => c.StartedAt)
                        .Select(c => (DateTime?)c.StartedAt)
                        .FirstOrDefault()
                })
                .OrderByDescending(t => t.CallsThisMonth)
                .ToListAsync();
            return Results.Ok(data);
        });
    }
}


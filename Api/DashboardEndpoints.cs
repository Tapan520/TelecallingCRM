using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/stats", async (TenantContext tc, AppDbContext db, IMemoryCache cache,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tid = tc.TenantId;

            var today = DateTime.UtcNow.Date;
            var rangeStart = from?.ToUniversalTime() ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var rangeEnd = to?.ToUniversalTime().Date.AddDays(1) ?? rangeStart.AddMonths(1);
            var cacheKey = $"dashboard-stats-{tid}-{from:yyyyMMdd}-{to:yyyyMMdd}";

            if (cache.TryGetValue(cacheKey, out var cached))
                return Results.Ok(cached);

            var totalLeads = await db.Leads.CountAsync(l => l.TenantId == tid);
            var newLeads = await db.Leads.CountAsync(l => l.TenantId == tid && l.Status == LeadStatus.New);
            var convertedLeads = await db.Leads.CountAsync(l => l.TenantId == tid && l.Status == LeadStatus.Converted);
            var todayCalls = await db.Calls.CountAsync(c => c.TenantId == tid && c.StartedAt >= today);
            var totalCallsInRange = await db.Calls.CountAsync(c => c.TenantId == tid && c.StartedAt >= rangeStart && c.StartedAt < rangeEnd);
            var avgCallDuration = await db.Calls
                .Where(c => c.TenantId == tid && c.DurationSeconds > 0)
                .AverageAsync(c => (double?)c.DurationSeconds) ?? 0;

            var leadsByStatus = await db.Leads
                .Where(l => l.TenantId == tid)
                .GroupBy(l => l.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();

            var callsLast7Days = await db.Calls
                .Where(c => c.TenantId == tid && c.StartedAt >= DateTime.UtcNow.AddDays(-7))
                .GroupBy(c => c.StartedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var topAgents = await db.Calls
                .Where(c => c.TenantId == tid && c.StartedAt >= DateTime.UtcNow.AddDays(-30))
                .GroupBy(c => c.AgentId)
                .Select(g => new {
                    AgentId = g.Key,
                    CallCount = g.Count(),
                    ConvertedCount = g.Count(c => c.Outcome == CallOutcome.Converted)
                })
                .OrderByDescending(x => x.CallCount)
                .Take(5)
                .Join(db.Users, a => a.AgentId, u => u.Id, (a, u) => new {
                    u.FullName, a.CallCount, a.ConvertedCount
                })
                .ToListAsync();

            var result = new {
                totalLeads, newLeads, convertedLeads,
                todayCalls, totalCallsInRange,
                avgCallDurationSeconds = (int)avgCallDuration,
                leadsByStatus, callsLast7Days, topAgents
            };

            cache.Set(cacheKey, result, TimeSpan.FromSeconds(60));
            return Results.Ok(result);
        });
    }
}


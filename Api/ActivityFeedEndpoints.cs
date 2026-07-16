using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class ActivityFeedEndpoints
{
    public static void MapActivityFeedEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/activity-feed")
            .WithTags("ActivityFeed")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] string? type, [FromQuery] Guid? leadId,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to,
            [FromQuery] bool myActivity = false,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var query = db.ActivityLogs
                .Where(a => a.TenantId == tc.TenantId)
                .AsQueryable();

            if (myActivity)
                query = query.Where(a => a.UserId == userId);
            if (leadId.HasValue)
                query = query.Where(a => a.LeadId == leadId);
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<TelecallingCRM.Data.Models.ActivityType>(type, out var at))
                query = query.Where(a => a.Type == at);
            if (from.HasValue)
                query = query.Where(a => a.OccurredAt >= from.Value.ToUniversalTime());
            if (to.HasValue)
                query = query.Where(a => a.OccurredAt <= to.Value.ToUniversalTime().AddDays(1));

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new {
                    a.Id, a.Type, a.Summary, a.OccurredAt,
                    a.LeadId,
                    LeadName = a.Lead.Name,
                    By = a.User.FullName,
                    ByUserId = a.UserId
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });
    }
}

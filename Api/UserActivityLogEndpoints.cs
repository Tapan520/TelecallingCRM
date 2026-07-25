using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class UserActivityLogEndpoints
{
    public static void MapUserActivityLogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/user-logs")
            .WithTags("UserLogs")
            .RequireAuthorization(p => p.RequireRole("admin", "manager", "superadmin"))
            .RequireRateLimiting("api");

        // GET logs with search, filter, pagination
        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? q,
            [FromQuery] string? module,
            [FromQuery] string? action,
            [FromQuery] Guid? userId,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var query = db.UserActivityLogs
                .Where(l => l.TenantId == tc.TenantId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(l =>
                    l.Description.Contains(q) ||
                    l.Action.Contains(q) ||
                    l.Module.Contains(q) ||
                    (l.User.FullName != null && l.User.FullName.Contains(q)) ||
                    (l.User.Email != null && l.User.Email.Contains(q)));

            if (!string.IsNullOrWhiteSpace(module))
                query = query.Where(l => l.Module == module);

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(l => l.Action == action);

            if (userId.HasValue)
                query = query.Where(l => l.UserId == userId.Value);

            if (DateTime.TryParse(from, out var fromDate))
                query = query.Where(l => l.CreatedAt >= fromDate);

            if (DateTime.TryParse(to, out var toDate))
                query = query.Where(l => l.CreatedAt < toDate.AddDays(1));

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new {
                    l.Id,
                    l.Action,
                    l.Module,
                    l.Description,
                    l.EntityId,
                    l.IpAddress,
                    l.CreatedAt,
                    UserName  = l.User.FullName,
                    UserEmail = l.User.Email,
                    UserRole  = l.User.Role
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // GET distinct modules for filter dropdown
        group.MapGet("/modules", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var modules = await db.UserActivityLogs
                .Where(l => l.TenantId == tc.TenantId)
                .Select(l => l.Module)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();
            return Results.Ok(modules);
        });

        // DELETE single log entry (admin only)
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            if (role != "admin" && role != "manager") return Results.Forbid();

            var log = await db.UserActivityLogs
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tc.TenantId);
            if (log == null) return Results.NotFound();

            db.UserActivityLogs.Remove(log);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // DELETE bulk — clear logs older than N days (admin only)
        group.MapDelete("/bulk", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] int olderThanDays = 90) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            if (role != "admin") return Results.Forbid();

            var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
            var deleted = await db.UserActivityLogs
                .Where(l => l.TenantId == tc.TenantId && l.CreatedAt < cutoff)
                .ExecuteDeleteAsync();

            return Results.Ok(new { deleted });
        });
    }
}

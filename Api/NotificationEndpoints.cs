using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] bool unreadOnly = false) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var query = db.Notifications.Where(n => n.UserId == userId);
            if (unreadOnly) query = query.Where(n => !n.IsRead);
            var results = await query.OrderByDescending(n => n.CreatedAt).Take(50)
                .Select(n => new { n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt })
                .ToListAsync();
            var unreadCount = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            return Results.Ok(new { unreadCount, notifications = results });
        });

        group.MapPost("/{id:guid}/read", async (Guid id, AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var n = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (n == null) return Results.NotFound();
            n.IsRead = true;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        group.MapPost("/read-all", async (AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            await db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
            return Results.Ok();
        });

        // GET unread count badge
        group.MapGet("/unread-count", async (AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            return Results.Ok(new { unreadCount = count });
        });
    }
}


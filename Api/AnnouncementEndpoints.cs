using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/announcements")
            .WithTags("Announcements")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET all active announcements
        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] bool includeExpired = false) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var now = DateTime.UtcNow;

            var query = db.Announcements
                .Where(a => a.TenantId == tc.TenantId && a.IsActive);

            if (!includeExpired)
                query = query.Where(a => a.ExpiresAt == null || a.ExpiresAt > now);

            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new {
                    a.Id, a.Title, a.Body, a.Priority, a.ExpiresAt, a.CreatedAt,
                    CreatedBy = a.CreatedBy.FullName,
                    IsRead = a.Reads.Any(r => r.UserId == userId)
                })
                .ToListAsync();

            return Results.Ok(items);
        });

        // GET unread count
        group.MapGet("/unread-count", async (TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Ok(new { count = 0 });
            var userId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var now = DateTime.UtcNow;
            var count = await db.Announcements
                .Where(a => a.TenantId == tc.TenantId && a.IsActive
                         && (a.ExpiresAt == null || a.ExpiresAt > now)
                         && !a.Reads.Any(r => r.UserId == userId))
                .CountAsync();
            return Results.Ok(new { count });
        });

        // POST create announcement (admin/manager)
        group.MapPost("/", async ([FromBody] CreateAnnouncementDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var ann = new Announcement
            {
                TenantId = tc.TenantId, CreatedById = userId,
                Title = dto.Title, Body = dto.Body,
                Priority = dto.Priority, ExpiresAt = dto.ExpiresAt
            };
            db.Announcements.Add(ann);
            await db.SaveChangesAsync();
            return Results.Created($"/api/announcements/{ann.Id}", new { ann.Id });
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));

        // POST mark as read
        group.MapPost("/{id:guid}/read", async (Guid id, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Ok();
            var userId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var exists = await db.AnnouncementReads
                .AnyAsync(r => r.AnnouncementId == id && r.UserId == userId);
            if (!exists)
            {
                db.AnnouncementReads.Add(new AnnouncementRead { AnnouncementId = id, UserId = userId });
                await db.SaveChangesAsync();
            }
            return Results.Ok();
        });

        // DELETE announcement (admin only)
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var ann = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tc.TenantId);
            if (ann == null) return Results.NotFound();
            ann.IsActive = false;
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));
    }
}

public record CreateAnnouncementDto(string Title, string Body, AnnouncementPriority Priority, DateTime? ExpiresAt);

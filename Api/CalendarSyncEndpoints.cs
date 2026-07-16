using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

/// <summary>Calendar Sync - stores OAuth tokens per user and provides status/disconnect.</summary>
public static class CalendarSyncEndpoints
{
    public static void MapCalendarSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/calendar-sync").WithTags("CalendarSync").RequireAuthorization().RequireRateLimiting("api");

        // GET /api/calendar-sync — get current user's sync config
        group.MapGet("/", async (AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var config = await db.CalendarSyncConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (config is null)
                return Results.Ok(new { connected = false, provider = (string?)null, syncFollowUps = true, syncMeetings = true });
            return Results.Ok(new {
                connected = config.Status == CalendarSyncStatus.Connected,
                config.Provider, config.Status,
                config.SyncFollowUps, config.SyncMeetings,
                config.TokenExpiresAt, config.CalendarId
            });
        });

        // POST /api/calendar-sync/connect — save OAuth tokens (called after OAuth callback)
        group.MapPost("/connect", async ([FromBody] CalendarConnectDto dto, AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var config = await db.CalendarSyncConfigs.FirstOrDefaultAsync(c => c.UserId == userId);
            if (config is null)
            {
                config = new CalendarSyncConfig { UserId = userId };
                db.CalendarSyncConfigs.Add(config);
            }
            config.Provider = dto.Provider;
            config.AccessToken = dto.AccessToken;
            config.RefreshToken = dto.RefreshToken;
            config.TokenExpiresAt = dto.TokenExpiresAt;
            config.CalendarId = dto.CalendarId;
            config.Status = CalendarSyncStatus.Connected;
            config.SyncFollowUps = dto.SyncFollowUps;
            config.SyncMeetings = dto.SyncMeetings;
            config.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { connected = true, config.Provider });
        });

        // DELETE /api/calendar-sync/disconnect
        group.MapDelete("/disconnect", async (AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var config = await db.CalendarSyncConfigs.FirstOrDefaultAsync(c => c.UserId == userId);
            if (config is null) return Results.NotFound();
            config.Status = CalendarSyncStatus.Disconnected;
            config.AccessToken = null;
            config.RefreshToken = null;
            config.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { connected = false });
        });

        // PATCH /api/calendar-sync/preferences
        group.MapPatch("/preferences", async ([FromBody] CalendarPrefsDto dto, AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var config = await db.CalendarSyncConfigs.FirstOrDefaultAsync(c => c.UserId == userId);
            if (config is null) return Results.NotFound();
            config.SyncFollowUps = dto.SyncFollowUps;
            config.SyncMeetings = dto.SyncMeetings;
            config.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // GET /api/calendar-sync/upcoming — return upcoming meetings+followups for calendar view
        group.MapGet("/upcoming", async (AppDbContext db, HttpContext http, TenantContext tc,
            [FromQuery] int days = 14) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var from = DateTime.UtcNow.Date;
            var to = from.AddDays(days);

            var meetings = await db.Meetings
                .Where(m => m.TenantId == tc.TenantId &&
                            (m.OrganisedById == userId || m.Attendees.Any(a => a.UserId == userId)) &&
                            m.ScheduledAt >= from && m.ScheduledAt < to)
                .Select(m => new CalendarEvent("meeting", m.Id.ToString(), m.Title, m.ScheduledAt, m.ScheduledAt.AddMinutes(m.DurationMinutes), m.Lead.Name))
                .ToListAsync();

            var followUps = await db.FollowUps
                .Where(f => f.TenantId == tc.TenantId && f.AssignedToId == userId &&
                            f.ScheduledAt >= from && f.ScheduledAt < to &&
                            f.Status == FollowUpStatus.Pending)
                .Select(f => new CalendarEvent("followup", f.Id.ToString(), f.Notes ?? "Follow-up", f.ScheduledAt, f.ScheduledAt.AddHours(1), f.Lead.Name))
                .ToListAsync();

            var events = meetings.Concat(followUps).OrderBy(e => e.Start).ToList();
            return Results.Ok(events);
        });
    }
}

public record CalendarConnectDto(CalendarProvider Provider, string AccessToken, string? RefreshToken,
    DateTime? TokenExpiresAt, string? CalendarId, bool SyncFollowUps, bool SyncMeetings);
public record CalendarPrefsDto(bool SyncFollowUps, bool SyncMeetings);
public record CalendarEvent(string Type, string Id, string Title, DateTime Start, DateTime End, string? LeadName);

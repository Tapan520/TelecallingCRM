using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class FollowUpEndpoints
{
    public static void MapFollowUpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/followups").WithTags("FollowUps").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? status, [FromQuery] string? date) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var query = db.FollowUps
                .Where(f => f.TenantId == tc.TenantId)
                .Include(f => f.Lead)
                .Include(f => f.AssignedTo)
                .AsQueryable();

            if (Enum.TryParse<FollowUpStatus>(status, true, out var fs))
                query = query.Where(f => f.Status == fs);

            if (DateOnly.TryParse(date, out var d))
                query = query.Where(f => f.ScheduledAt.Date == d.ToDateTime(TimeOnly.MinValue).Date);

            var results = await query
                .OrderBy(f => f.ScheduledAt)
                .Select(f => new {
                    f.Id, f.ScheduledAt, f.Channel, f.Status, f.Notes, f.IsRecurring,
                    LeadName = f.Lead.Name, LeadPhone = f.Lead.Phone, LeadId = f.LeadId,
                    AssignedTo = f.AssignedTo.FullName
                })
                .ToListAsync();
            return Results.Ok(results);
        });

        group.MapGet("/today", async (TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var results = await db.FollowUps
                .Where(f => f.TenantId == tc.TenantId && f.AssignedToId == userId
                         && f.ScheduledAt >= today && f.ScheduledAt < tomorrow
                         && f.Status == FollowUpStatus.Pending)
                .Include(f => f.Lead)
                .OrderBy(f => f.ScheduledAt)
                .Select(f => new { f.Id, f.ScheduledAt, f.Channel, f.Notes, LeadName = f.Lead.Name, LeadPhone = f.Lead.Phone, f.LeadId })
                .ToListAsync();
            return Results.Ok(results);
        });

        group.MapPost("/", async ([FromBody] FollowUpUpsertDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var followup = new FollowUp
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                AssignedToId = dto.AssignedToId ?? userId,
                ScheduledAt = dto.ScheduledAt,
                Channel = dto.Channel,
                Notes = dto.Notes,
                IsRecurring = dto.IsRecurring,
                RecurrenceRule = dto.RecurrenceRule
            };
            db.FollowUps.Add(followup);

            // Update lead next follow-up date
            var lead = await db.Leads.FindAsync(dto.LeadId);
            if (lead != null) { lead.NextFollowUpAt = dto.ScheduledAt; lead.Status = LeadStatus.FollowUp; }

            // Activity log
            db.ActivityLogs.Add(new ActivityLog {
                TenantId = tc.TenantId, LeadId = dto.LeadId, UserId = userId,
                Type = ActivityType.FollowUpScheduled,
                Summary = $"Follow-up scheduled via {dto.Channel} on {dto.ScheduledAt:dd MMM yyyy HH:mm}"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/api/followups/{followup.Id}", followup);
        });

        group.MapPost("/{id:guid}/complete", async (Guid id, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var f = await db.FollowUps.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tc.TenantId);
            if (f == null) return Results.NotFound();
            f.Status = FollowUpStatus.Done;
            f.CompletedAt = DateTime.UtcNow;

            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            db.ActivityLogs.Add(new ActivityLog {
                TenantId = tc.TenantId, LeadId = f.LeadId, UserId = userId,
                Type = ActivityType.FollowUpCompleted,
                Summary = $"Follow-up completed (was scheduled {f.ScheduledAt:dd MMM yyyy})"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { f.Status, f.CompletedAt });
        });

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var f = await db.FollowUps.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tc.TenantId);
            if (f == null) return Results.NotFound();
            db.FollowUps.Remove(f);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record FollowUpUpsertDto(
    Guid LeadId, DateTime ScheduledAt, FollowUpChannel Channel,
    string? Notes, bool IsRecurring, string? RecurrenceRule, Guid? AssignedToId);

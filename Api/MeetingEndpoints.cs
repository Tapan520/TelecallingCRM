using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class MeetingEndpoints
{
    public static void MapMeetingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/meetings")
            .WithTags("Meetings")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET — list meetings (optionally filter by leadId or date range)
        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? leadId, [FromQuery] string? status,
            [FromQuery] string? from, [FromQuery] string? to,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var query = db.Meetings
                .Where(m => m.TenantId == tc.TenantId)
                .AsQueryable();

            if (leadId.HasValue) query = query.Where(m => m.LeadId == leadId);

            if (Enum.TryParse<MeetingStatus>(status, true, out var ms))
                query = query.Where(m => m.Status == ms);

            if (DateTime.TryParse(from, out var fromDt))
                query = query.Where(m => m.ScheduledAt >= fromDt);

            if (DateTime.TryParse(to, out var toDt))
                query = query.Where(m => m.ScheduledAt <= toDt);

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(m => m.ScheduledAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(m => new {
                    m.Id, m.Title, m.Type, m.Status, m.ScheduledAt, m.DurationMinutes,
                    m.Location, m.MeetingLink, m.Notes, m.Outcome,
                    LeadName = m.Lead.Name, LeadPhone = m.Lead.Phone, m.LeadId,
                    OrganisedBy = m.OrganisedBy.FullName,
                    AttendeeCount = m.Attendees.Count
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // GET — today's meetings for the current user
        group.MapGet("/today", async (TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var items = await db.Meetings
                .Where(m => m.TenantId == tc.TenantId
                         && m.ScheduledAt >= today && m.ScheduledAt < tomorrow
                         && (m.OrganisedById == userId
                             || m.Attendees.Any(a => a.UserId == userId)))
                .OrderBy(m => m.ScheduledAt)
                .Select(m => new {
                    m.Id, m.Title, m.Type, m.Status, m.ScheduledAt,
                    m.DurationMinutes, m.Location, m.MeetingLink,
                    LeadName = m.Lead.Name, m.LeadId
                })
                .ToListAsync();

            return Results.Ok(items);
        });

        // GET — single meeting detail
        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var m = await db.Meetings
                .Include(m => m.Lead)
                .Include(m => m.OrganisedBy)
                .Include(m => m.Attendees).ThenInclude(a => a.User)
                .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tc.TenantId);
            if (m == null) return Results.NotFound();

            return Results.Ok(new {
                m.Id, m.Title, m.Agenda, m.Type, m.Status,
                m.ScheduledAt, m.DurationMinutes, m.Location, m.MeetingLink,
                m.Notes, m.Outcome, m.CreatedAt,
                Lead = new { m.Lead.Id, m.Lead.Name, m.Lead.Phone, m.Lead.Email },
                OrganisedBy = m.OrganisedBy.FullName,
                Attendees = m.Attendees.Select(a => new { a.UserId, a.User.FullName })
            });
        });

        // POST — schedule a meeting
        group.MapPost("/", async ([FromBody] MeetingUpsertDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var meeting = new Meeting
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                OrganisedById = userId,
                Title = dto.Title,
                Agenda = dto.Agenda,
                Type = dto.Type,
                ScheduledAt = dto.ScheduledAt,
                DurationMinutes = dto.DurationMinutes,
                Location = dto.Location,
                MeetingLink = dto.MeetingLink,
                Notes = dto.Notes
            };
            db.Meetings.Add(meeting);

            // Add organiser as attendee automatically
            db.MeetingAttendees.Add(new MeetingAttendee { Meeting = meeting, UserId = userId });

            // Add extra attendees
            if (dto.AttendeeIds != null)
                foreach (var aId in dto.AttendeeIds.Where(a => a != userId))
                    db.MeetingAttendees.Add(new MeetingAttendee { Meeting = meeting, UserId = aId });

            // Activity log
            db.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tc.TenantId, LeadId = dto.LeadId, UserId = userId,
                Type = ActivityType.MeetingScheduled,
                Summary = $"Meeting \"{dto.Title}\" scheduled on {dto.ScheduledAt:dd MMM yyyy HH:mm}"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/api/meetings/{meeting.Id}", new { meeting.Id });
        });

        // PUT — update a meeting
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] MeetingUpsertDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var meeting = await db.Meetings
                .Include(m => m.Attendees)
                .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tc.TenantId);
            if (meeting == null) return Results.NotFound();

            meeting.Title = dto.Title;
            meeting.Agenda = dto.Agenda;
            meeting.Type = dto.Type;
            meeting.ScheduledAt = dto.ScheduledAt;
            meeting.DurationMinutes = dto.DurationMinutes;
            meeting.Location = dto.Location;
            meeting.MeetingLink = dto.MeetingLink;
            meeting.Notes = dto.Notes;

            if (dto.AttendeeIds != null)
            {
                db.MeetingAttendees.RemoveRange(meeting.Attendees);
                foreach (var aId in dto.AttendeeIds)
                    db.MeetingAttendees.Add(new MeetingAttendee { MeetingId = id, UserId = aId });
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // POST — mark meeting as completed with outcome
        group.MapPost("/{id:guid}/complete", async (Guid id, [FromBody] CompleteMeetingDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var meeting = await db.Meetings
                .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tc.TenantId);
            if (meeting == null) return Results.NotFound();
            var userId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            meeting.Status = MeetingStatus.Completed;
            meeting.Outcome = dto.Outcome;
            meeting.Notes = string.IsNullOrEmpty(dto.Notes) ? meeting.Notes : dto.Notes;

            db.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tc.TenantId, LeadId = meeting.LeadId, UserId = userId,
                Type = ActivityType.MeetingCompleted,
                Summary = $"Meeting \"{meeting.Title}\" completed. Outcome: {dto.Outcome}"
            });

            await db.SaveChangesAsync();
            return Results.Ok(new { meeting.Status, meeting.Outcome });
        });

        // POST — cancel meeting
        group.MapPost("/{id:guid}/cancel", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var meeting = await db.Meetings
                .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tc.TenantId);
            if (meeting == null) return Results.NotFound();
            meeting.Status = MeetingStatus.Cancelled;
            await db.SaveChangesAsync();
            return Results.Ok(new { meeting.Status });
        });

        // DELETE
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var meeting = await db.Meetings
                .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tc.TenantId);
            if (meeting == null) return Results.NotFound();
            db.Meetings.Remove(meeting);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record MeetingUpsertDto(
    Guid LeadId, string Title, string? Agenda,
    MeetingType Type, DateTime ScheduledAt, int DurationMinutes,
    string? Location, string? MeetingLink, string? Notes,
    List<Guid>? AttendeeIds);

public record CompleteMeetingDto(string? Outcome, string? Notes);
